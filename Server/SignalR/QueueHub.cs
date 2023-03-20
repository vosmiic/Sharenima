using System.Security.Claims;
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

namespace Sharenima.Server.SignalR;

public class QueueHub : Hub {
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
        var httpContext = Context.GetHttpContext();
        if (httpContext == null) return;
        var instanceId = httpContext.Request.Query["instanceId"];
        if (instanceId.Count == 0) return;
        Guid parsedInstanceId = Guid.Parse(instanceId[0]);
        string? username = null;
        int leaderRank = 0;
        if (Context.User?.IsAuthenticated() == true) {
            await using var context = await _applicationDbContextFactory.CreateDbContextAsync();
            ApplicationUser? user = context.Users.Include(au => au.Roles).FirstOrDefault(user => user.Id == userId);
            if (user == null) return;
            username = user.UserName;
            leaderRank = user.Roles.Any(role => role.InstanceId == parsedInstanceId && role.Permission == Permissions.Permission.Administrator) ? 2 : 1;
        }

        if (_instanceTimeTracker.GetInstanceTime(parsedInstanceId) == null) {
            await using var context = await _contextFactory.CreateDbContextAsync();
            Instance? instance = await context.Instances.FirstOrDefaultAsync(instance => instance.Id == parsedInstanceId);
            if (instance == null) return;
            _instanceTimeTracker.Add(parsedInstanceId, instance.VideoTime);
        }

        _connectionMapping.Add(parsedInstanceId, Context.ConnectionId, leaderRank, parsedUserId, username);
        _logger.LogInformation($"Added {(parsedUserId != null ? $"user {parsedUserId}" : "anonymous user")} to instance {instanceId[0]} connection map");
        if (parsedUserId != null) await Clients.Group(instanceId[0]).SendAsync("UserJoined", parsedUserId.Value);
    }

    public override async Task OnDisconnectedAsync(Exception? exception) {
        string? userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? parsedUserId = userId != null ? Guid.Parse(userId) : null;
        var httpContext = Context.GetHttpContext();
        if (httpContext == null) return;
        var instanceId = httpContext.Request.Query["instanceId"];
        if (instanceId.Count == 0) return;

        _connectionMapping.Remove(Guid.Parse(instanceId[0]), Context.ConnectionId, parsedUserId);
        _logger.LogInformation($"Removed {(parsedUserId != null ? $"user {parsedUserId}" : "anonymous user")} from instance {instanceId[0]} connection map");
        if (parsedUserId != null) await Clients.Group(instanceId[0]).SendAsync("UserLeft", parsedUserId.Value);
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
                if (queue.VideoType == VideoType.FileUpload) {
                    FileHelper.DeleteFile(queue.Url, _configuration, _logger);
                    if (queue.Thumbnail != null)
                        FileHelper.DeleteFile(queue.Thumbnail, _configuration, _logger);
                }

                if (instance != null) {
                    instance.VideoTime = TimeSpan.Zero;
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
        if ((!seeked && instanceConnection is { IsLeader: true } && storedInstanceTimeDifference is not (> 1000 or < -1000))
            || seeked) {
            await using var context = await _contextFactory.CreateDbContextAsync();
            if (videoId == null || context.Queues.FirstOrDefault(queue => queue.Id == videoId) == null) return;
            Instance? instance = await context.Instances.FirstOrDefaultAsync(instance => instance.Id == groupName && instance.PlayerState != State.Ended);
            if (instance == null) return;
            instance.VideoTime = videoTime;
            _instanceTimeTracker.Update(instance.Id, videoTime, true);
            await context.SaveChangesAsync();

            await Clients.Group(groupName.ToString()).SendAsync("ReceiveProgressChange", videoTime);
        } else {
            // something weird is happening, rewind the user
            await Clients.Caller.SendAsync("ReceiveProgressChange", _instanceTimeTracker.GetInstanceTime(groupName));
        }
    }

    public string? GetConfigurationValue(ConfigKey configKey) => _configuration[$"Stream:{configKey.ToString()}"];
}