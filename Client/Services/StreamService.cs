using AsyncEventHandlers;

namespace Sharenima.Client;

public class StreamService {
    private readonly AsyncEventHandler watchStream = new();
    private readonly AsyncEventHandler closeStream = new();
    public string streamUsername { get; set; }

    public event AsyncEvent WatchStream {
        add { watchStream.Register(value); }
        remove { watchStream.Unregister(value); }
    }
    
    public event AsyncEvent CloseStream {
        add { closeStream.Register(value); }
        remove { closeStream.Unregister(value); }
    }

    public async Task CallWatchStream(string username) {
        streamUsername = username;
        await watchStream.InvokeAsync();
    }

    public async Task CallCloseStream() {
        await closeStream.InvokeAsync();
    }
}