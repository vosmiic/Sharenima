using AsyncEventHandlers;

namespace Sharenima.Client; 

public class HubService {
    private readonly AsyncEventHandler<bool> leadershipChange = new();
    public event AsyncEvent<bool> LeadershipChange
    {
        add { leadershipChange.Register(value); }
        remove { leadershipChange.Unregister(value); }
    }
    
    public async Task LeadershipChanged(bool userIsLeader) {
        await leadershipChange.InvokeAsync(userIsLeader);
    }
}