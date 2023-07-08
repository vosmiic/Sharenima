using AsyncEventHandlers;

namespace Sharenima.Client; 

public class HubService {
    public bool IsLeader { get; private set; }
    private readonly AsyncEventHandler<bool> leadershipChange = new();
    public event AsyncEvent<bool> LeadershipChange
    {
        add { leadershipChange.Register(value); }
        remove { leadershipChange.Unregister(value); }
    }
    
    public async Task LeadershipChanged(bool userIsLeader) {
        IsLeader = userIsLeader;
        Console.WriteLine(IsLeader ? "I am the leader :)" : "I am no longer the leader :(");
        //await leadershipChange.InvokeAsync(userIsLeader);
    }
}