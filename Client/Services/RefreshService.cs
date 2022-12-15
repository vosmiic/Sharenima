namespace Sharenima.Client;

public class RefreshService {
    public event Action InstanceIndexRefreshRequested;
    public event Action PlayerRefreshRequested;
    public event Action PlayerVideoEnded;

    public void CallInstanceIndexRefresh() {
        InstanceIndexRefreshRequested?.Invoke();
    }

    public void CallPlayerRefreshRequested() {
        PlayerRefreshRequested?.Invoke();
    }

    public void CallPlayerVideoEnded() {
        PlayerVideoEnded?.Invoke();
    }
}