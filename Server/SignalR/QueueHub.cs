using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Data;
using Sharenima.Shared;

namespace Sharenima.Server.SignalR; 

public class QueueHub : Hub {
    private readonly IDbContextFactory<GeneralDbContext> _contextFactory;

    public QueueHub(IDbContextFactory<GeneralDbContext> contextFactory) {
        _contextFactory = contextFactory;
    }

    public async Task JoinGroup(string groupName) {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task SendStateChange(string groupName, int state) {
        await Clients.Group(groupName).SendAsync("ReceiveStateChange", state);
    }

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