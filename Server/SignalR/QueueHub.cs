using System.Security.Claims;
using Common.Extensions;
using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sharenima.Server.Data;
using Sharenima.Server.Helpers;
using Sharenima.Server.Models;
using Sharenima.Server.Services;
using Sharenima.Shared;
using Sharenima.Shared.Configuration;
using Sharenima.Shared.Queue;

namespace Sharenima.Server.SignalR;

public class QueueHub : BaseHub {
    private readonly IDbContextFactory<GeneralDbContext> _contextFactory;
    private readonly IDbContextFactory<ApplicationDbContext> _applicationDbContextFactory;
    private readonly ConnectionMapping _connectionMapping;
    private readonly ILogger<QueueHub> _logger;
    private readonly IConfiguration _configuration;
    private readonly InstanceTimeTracker _instanceTimeTracker;

    public QueueHub(IDbContextFactory<GeneralDbContext> contextFactory, ConnectionMapping connectionMapping, ILogger<QueueHub> logger, IMemoryCache memoryCache, IConfiguration configuration, IDbContextFactory<ApplicationDbContext> applicationDbContextFactory, InstanceTimeTracker instanceTimeTracker) {
        _contextFactory = contextFactory;
        _connectionMapping = connectionMapping;
        _logger = logger;
        _configuration = configuration;
        _applicationDbContextFactory = applicationDbContextFactory;
        _instanceTimeTracker = instanceTimeTracker;
    }

    public override async Task OnConnectedAsync() {
        string? userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? parsedUserId = userId != null ? Guid.Parse(userId) : null;
        string? username = null;
        int leaderRank = 0;
        if (InstanceId == null) return;
        if (Context.User?.IsAuthenticated() == true) {
            await using var context = await _applicationDbContextFactory.CreateDbContextAsync();
            ApplicationUser? user = context.Users.Include(au => au.Roles).FirstOrDefault(user => user.Id == userId);
            if (user == null) return;
            username = user.UserName;
            leaderRank = user.Roles.Any(role => role.InstanceId == InstanceId && role.Permission == Permissions.Permission.Administrator) ? 2 : 1;
        }

        if (_instanceTimeTracker.GetInstanceTime(InstanceId.Value) == null) {
            await using var context = await _contextFactory.CreateDbContextAsync();
            Instance? instance = await context.Instances.FirstOrDefaultAsync(instance => instance.Id == InstanceId);
            if (instance == null) return;
            _instanceTimeTracker.Add(InstanceId.Value, instance.VideoTime);
        }

        if (username == null) {
            username = "Anonymous";
            var words = gnuciDictionary.EnglishDictionary.GetAllWords().ToList();
            for (int i = 0; i < 2; i++) {
                string word = words.Random().Value;
                if (!char.IsUpper(word[0])) {
                    if (word.Length > 1)
                        username += char.ToUpper(word[0]) + word.Substring(1);
                    else
                        username += char.ToUpper(word[0]);
                } else {
                    username += word;
                }
            }
        }
        
        (string? oldLeader, string? newLeader) instanceLeadership = _connectionMapping.Add(InstanceId.Value, Context.ConnectionId, leaderRank, parsedUserId, username);
        await LeadershipChange(instanceLeadership.oldLeader, instanceLeadership.newLeader);
        _logger.LogInformation($"Added {username} to instance {InstanceId} connection map");
        await Clients.Group(InstanceId.Value.ToString()).SendAsync("UserJoined", username);
        await Clients.Caller.SendAsync("UserJoined", username);
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception) {
        string? userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? parsedUserId = userId != null ? Guid.Parse(userId) : null;
        var httpContext = Context.GetHttpContext();
        if (httpContext == null) return;
        var instanceId = httpContext.Request.Query["instanceId"];
        if (instanceId.Count == 0) return;

        Guid parsedInstanceId = Guid.Parse(instanceId[0]);
        ConnectionMapping.InstanceConnection? user = _connectionMapping.GetConnectionById(parsedInstanceId, Context.ConnectionId);
        if (user == null) return;
        string? newLeader = _connectionMapping.Remove(parsedInstanceId, Context.ConnectionId, parsedUserId);
        await LeadershipChange(null, newLeader);
        _logger.LogInformation($"Removed {user.UserName} from instance {instanceId[0]} connection map");
        await Clients.Group(instanceId[0]).SendAsync("UserLeft", user.UserName);
    }

    private async Task LeadershipChange(string? oldLeader = null, string? newLeader = null) {
        if (oldLeader != null)
            await Clients.Client(oldLeader).SendAsync("LeadershipChange", false);
        if (newLeader != null)
            await Clients.Client(newLeader).SendAsync("LeadershipChange", true);
    }

    public async Task JoinGroup(string groupName) {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    [Authorize(Policy = "ChangeProgress")]
    public async Task SendStateChange(string groupName, State playerState, Guid queueId) {
        await using var context = await _contextFactory.CreateDbContextAsync();

        Queue? queue = context.Queues.FirstOrDefault(queue => queue.Id == queueId);
        if (queue != null) {
            Instance? instance = await context.Instances.FirstOrDefaultAsync(instance => instance.Id == queue.InstanceId);
            if (playerState == State.Ended) {
                context.Remove(queue);
                SortQueueOrder(context, queue.InstanceId);
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
                    if (queue.VideoType == VideoType.FileUpload) {
                        FileHelper.DeleteFile(queue.Url, _configuration, _logger);
                        if (queue.Thumbnail != null)
                            FileHelper.DeleteFile(queue.Thumbnail, _configuration, _logger);
                    }

                if (instance != null) {
                    instance.VideoTime = TimeSpan.Zero;
                    _instanceTimeTracker.Update(instance.Id, TimeSpan.Zero, true);
                }
            }

            if (instance != null) {
                _logger.LogInformation($"Updating instance {instance.Id} state in database");
                instance.PlayerState = playerState;
            }

            await context.SaveChangesAsync();
            await Clients.Group(groupName).SendAsync("ReceiveStateChange", playerState);
        }
    }

    [Authorize(Policy = "ChangeProgress")]
    public async Task SendProgressChange(Guid groupName, TimeSpan videoTime, bool seeked, Guid? videoId) {
        ConnectionMapping.InstanceConnection? instanceConnection = _connectionMapping.GetConnectionById(groupName, Context.ConnectionId);
        double? storedInstanceTimeDifference = _instanceTimeTracker.GetInstanceTime(groupName)?.TotalMilliseconds - videoTime.TotalMilliseconds;
        if ((!seeked && instanceConnection is { IsLeader: true } && storedInstanceTimeDifference is not (> 1500 or < -1500))
            || seeked) {
            await using var context = await _contextFactory.CreateDbContextAsync();
            if (videoId == null || context.Queues.FirstOrDefault(queue => queue.Id == videoId) == null) return;
            Instance? instance = await context.Instances.FirstOrDefaultAsync(instance => instance.Id == groupName && instance.PlayerState != State.Ended);
            if (instance == null) return;
            instance.VideoTime = videoTime;
            _instanceTimeTracker.Update(instance.Id, videoTime, true);
            await context.SaveChangesAsync();

            await Clients.Group(groupName.ToString()).SendAsync("ReceiveProgressChange", videoTime, videoId);
        } else {
            // something weird is happening, rewind the user
            await Clients.Caller.SendAsync("ReceiveProgressChange", _instanceTimeTracker.GetInstanceTime(groupName));
        }
    }

    private void SortQueueOrder(GeneralDbContext generalDbContext, Guid instanceId) {
        var queues = generalDbContext.Queues.Where(queue => queue.InstanceId == instanceId).AsEnumerable().Where(queue => generalDbContext.Entry(queue).State != EntityState.Deleted).OrderBy(queue => queue.Order).ToList();

        for (int i = 0; i < queues.Count(); i++) {
            if (i == 0) {
                queues[i].Order = 0;
                continue;
            }

            int previousOrder = queues[i - 1].Order;
            queues[i].Order = previousOrder + 1;
        }
    }

    public void RevokeLeadership(Guid groupName) {
        _connectionMapping.RevokeLeadership(groupName, Context.ConnectionId, true);
    }

    public void EnableLeader(Guid groupName, bool attemptToPromote) {
        _connectionMapping.EnableLeader(groupName, Context.ConnectionId, attemptToPromote);
    }

    public string? GetConfigurationValue(ConfigKey configKey) => _configuration[$"Stream:{configKey.ToString()}"];
}