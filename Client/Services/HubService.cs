using AsyncEventHandlers;

namespace Sharenima.Client; 

public class HubService {
    public bool IsLeader { get; private set; }
    private readonly AsyncEventHandler<string> userJoinRequested = new();
    public event AsyncEvent<string> userJoined
    {
        add => userJoinRequested.Register(value);
        remove => userJoinRequested.Unregister(value);
    }
    
    public async Task LeadershipChanged(bool userIsLeader) {
        IsLeader = userIsLeader;
    }

    public async Task UserJoined(string username) {
        await userJoinRequested.InvokeAsync(username);
    }
}