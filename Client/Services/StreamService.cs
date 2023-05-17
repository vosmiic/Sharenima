using AsyncEventHandlers;

namespace Sharenima.Client;

public class StreamService {
    private readonly AsyncEventHandler watchStream = new();
    public string streamUsername { get; set; }

    public event AsyncEvent WatchStream {
        add { watchStream.Register(value); }
        remove { watchStream.Unregister(value); }
    }

    public async Task CallWatchStream(string username) {
        streamUsername = username;
        await watchStream.InvokeAsync();
    }
}