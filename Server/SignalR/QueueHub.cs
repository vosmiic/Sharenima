using System.Security.Claims;
using Common.Extensions;
using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Data;
using Sharenima.Server.Models;
using Sharenima.Server.Services;
using Sharenima.Shared;
using Sharenima.Shared.Configuration;
using StackExchange.Redis;

namespace Sharenima.Server.SignalR;

public class QueueHub : BaseHub {
    private readonly IDbContextFactory<GeneralDbContext> _contextFactory;
    private readonly IDbContextFactory<ApplicationDbContext> _applicationDbContextFactory;
    private readonly ConnectionMapping _connectionMapping;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<QueueHub> _logger;
    private readonly IConfiguration _configuration;

    public QueueHub(IDbContextFactory<GeneralDbContext> contextFactory, ConnectionMapping connectionMapping, 
        IConfiguration configuration, IDbContextFactory<ApplicationDbContext> applicationDbContextFactory,
        ILoggerFactory loggerFactory, IConnectionMultiplexer connectionMultiplexer) {
        _contextFactory = contextFactory;
        _connectionMapping = connectionMapping;
        _logger = loggerFactory.CreateLogger<QueueHub>();
        _configuration = configuration;
        _applicationDbContextFactory = applicationDbContextFactory;
        _loggerFactory = loggerFactory;
        _connectionMultiplexer = connectionMultiplexer;
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
    public async Task SendProgressChange(Guid groupName, TimeSpan videoTime, bool seeked, Guid? videoId) {
        ConnectionMapping.InstanceConnection? instanceConnection = _connectionMapping.GetConnectionById(groupName, Context.ConnectionId);
        InstanceTimeTracker instanceTimeTracker = new InstanceTimeTracker(_loggerFactory, _connectionMultiplexer);
        double? storedInstanceTimeDifference = instanceTimeTracker.GetInstanceTime(groupName)?.TotalMilliseconds - videoTime.TotalMilliseconds;
        if ((!seeked && instanceConnection is { IsLeader: true } && storedInstanceTimeDifference is not (> 1500 or < -1500))
            || seeked) {
            await using var context = await _contextFactory.CreateDbContextAsync();
            if (videoId == null || context.Queues.FirstOrDefault(queue => queue.Id == videoId) == null) return;
            Instance? instance = await context.Instances.FirstOrDefaultAsync(instance => instance.Id == groupName && instance.PlayerState != State.Ended);
            if (instance == null) return;
            instanceTimeTracker.Upsert(instance.Id, videoTime);
            await context.SaveChangesAsync();

            await Clients.Group(groupName.ToString()).SendAsync("ReceiveProgressChange", videoTime, videoId);
        } else {
            // something weird is happening, rewind the user
            await Clients.Caller.SendAsync("ReceiveProgressChange", instanceTimeTracker.GetInstanceTime(groupName));
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