using Microsoft.AspNetCore.SignalR;

namespace Sharenima.Server.SignalR; 

public class QueueHub : Hub {
    public async Task JoinGroup(string groupName) {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task SendStateChange(string groupName, int state) {
        await Clients.Group(groupName).SendAsync("ReceiveStateChange", state);
    }
}