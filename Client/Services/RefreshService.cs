using AsyncEventHandlers;

namespace Sharenima.Client;

public class RefreshService {
    private readonly AsyncEventHandler instanceIndexRefreshRequested = new();
    public event AsyncEvent InstanceIndexRefreshRequested
    {
        add { instanceIndexRefreshRequested.Register(value); }
        remove { instanceIndexRefreshRequested.Unregister(value); }
    }
    
    private readonly AsyncEventHandler playerRefreshRequested = new();
    public event AsyncEvent PlayerRefreshRequested
    {
        add { playerRefreshRequested.Register(value); }
        remove { playerRefreshRequested.Unregister(value); }
    }
    
    private readonly AsyncEventHandler playerVideoEnded = new();
    public event AsyncEvent PlayerVideoEnded
    {
        add { playerVideoEnded.Register(value); }
        remove { playerVideoEnded.Unregister(value); }
    }

    public async Task CallInstanceIndexRefresh() {
        await instanceIndexRefreshRequested.InvokeAsync();
    }

    public async Task CallPlayerRefreshRequested() {
        await playerRefreshRequested.InvokeAsync();
    }

    public async Task CallPlayerVideoEnded() {
        await playerVideoEnded.InvokeAsync();
    }
}