using Microsoft.AspNetCore.SignalR;

namespace Sharenima.Server.SignalR; 

public class QueueHub : Hub {
    public async Task JoinGroup(string groupName) {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }
}