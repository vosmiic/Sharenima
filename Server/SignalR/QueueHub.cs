using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Data;
using Sharenima.Shared;

namespace Sharenima.Server.SignalR; 

public class QueueHub : Hub {
    private readonly IDbContextFactory<GeneralDbContext> _contextFactory;
    private readonly ConnectionMapping _connectionMapping;

    public QueueHub(IDbContextFactory<GeneralDbContext> contextFactory, ConnectionMapping connectionMapping) {
        _contextFactory = contextFactory;
        _connectionMapping = connectionMapping;
    }

    public override Task OnConnectedAsync() {
        string? userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var httpContext = Context.GetHttpContext();
        if (httpContext == null) return Task.CompletedTask;
        var instanceId = httpContext.Request.Query["instanceId"];
        if (instanceId.Count == 0) return Task.CompletedTask;

        _connectionMapping.Add(Guid.Parse(instanceId[0]), Context.ConnectionId, userId != null ? Guid.Parse(userId) : null, userId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception) {
        string? userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var httpContext = Context.GetHttpContext();
        if (httpContext == null) return Task.CompletedTask;
        var instanceId = httpContext.Request.Query["instanceId"];
        if (instanceId.Count == 0) return Task.CompletedTask;

        _connectionMapping.Remove(Guid.Parse(instanceId[0]), Context.ConnectionId, userId != null ? Guid.Parse(userId) : null);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task JoinGroup(string groupName) {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    [Authorize(Policy = "ChangeProgress")]
    public async Task SendStateChange(string groupName, int state, Guid queueId) {
        if (state == 0) {
            await using var context = await _contextFactory.CreateDbContextAsync();
            Queue? queue = context.Queues.FirstOrDefault(queue => queue.Id == queueId);
            if (queue != null) {
                Instance? instance = await context.Instances.FirstOrDefaultAsync(instance => instance.Id == queue.InstanceId);
                //context.Remove(queue);
                if (instance != null) {
                    instance.VideoTime = TimeSpan.Zero;
                }
                await context.SaveChangesAsync();
            }
        }
        await Clients.Group(groupName).SendAsync("ReceiveStateChange", state);
    }

    [Authorize(Policy = "ChangeProgress")]
    public async Task SendProgressChange(string groupName, TimeSpan videoTime) {
        await using var context = await _contextFactory.CreateDbContextAsync();
        Guid parsedGroupName;
        Instance? instance = await context.Instances.FirstOrDefaultAsync(instance => Guid.TryParse(groupName, out parsedGroupName) && instance.Id == parsedGroupName);
        if (instance == null) return;
        instance.VideoTime = videoTime;
        await context.SaveChangesAsync();

        await Clients.Group(groupName).SendAsync("ReceiveProgressChange", videoTime);
    }
}