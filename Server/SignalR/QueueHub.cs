using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sharenima.Server.Data;
using Sharenima.Shared;

namespace Sharenima.Server.SignalR;

public class QueueHub : Hub {
    private readonly IDbContextFactory<GeneralDbContext> _contextFactory;
    private readonly ConnectionMapping _connectionMapping;
    private readonly ILogger<QueueHub> _logger;
    private readonly IMemoryCache _memoryCache;

    public QueueHub(IDbContextFactory<GeneralDbContext> contextFactory, ConnectionMapping connectionMapping, ILogger<QueueHub> logger, IMemoryCache memoryCache) {
        _contextFactory = contextFactory;
        _connectionMapping = connectionMapping;
        _logger = logger;
        _memoryCache = memoryCache;
    }

    public override async Task OnConnectedAsync() {
        string? userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? parsedUserId = userId != null ? Guid.Parse(userId) : null;
        var httpContext = Context.GetHttpContext();
        if (httpContext == null) return;
        var instanceId = httpContext.Request.Query["instanceId"];
        if (instanceId.Count == 0) return;

        _connectionMapping.Add(Guid.Parse(instanceId[0]), Context.ConnectionId, parsedUserId, userId);
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
                //context.Remove(queue);
                if (instance != null) {
                    instance.VideoTime = TimeSpan.Zero;
                }
            }

            if (instance != null) {
                _logger.LogInformation($"Updating instance {instance.Id} state in database");
                instance.PlayerState = playerState;
            }
            await context.SaveChangesAsync();
        }

        await Clients.Group(groupName).SendAsync("ReceiveStateChange", playerState);
    }

    [Authorize(Policy = "ChangeProgress")]
    public async Task SendProgressChange(string groupName, TimeSpan videoTime, bool seeked) {
        if (seeked ||
            !_memoryCache.TryGetValue($"lastUpdate-{groupName}", out DateTime lastUpdate) ||
            DateTime.UtcNow > lastUpdate.AddMilliseconds(300)) {
            _memoryCache.Set($"lastUpdate-{groupName}", DateTime.UtcNow, TimeSpan.FromSeconds(2));
            await using var context = await _contextFactory.CreateDbContextAsync();
            Guid parsedGroupName;
            Instance? instance = await context.Instances.FirstOrDefaultAsync(instance => Guid.TryParse(groupName, out parsedGroupName) && instance.Id == parsedGroupName);
            if (instance == null) return;
            instance.VideoTime = videoTime;
            await context.SaveChangesAsync();

            await Clients.Group(groupName).SendAsync("ReceiveProgressChange", videoTime);
        }
    }
}