using Microsoft.AspNetCore.SignalR.Client;

namespace Sharenima.Client.Helpers; 

public class HubStarterConnections {
    public static void AttachHandlers(HubService hubService, HubConnection hubConnection) {
        hubConnection.On<bool>("LeadershipChange", async (userIsLeader) => {
            await hubService.LeadershipChanged(userIsLeader);
        });

        hubConnection.On<string>("UserJoined", async (username) => {
            await hubService.UserJoined(username);
        });
    }
}