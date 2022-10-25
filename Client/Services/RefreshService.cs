namespace Sharenima.Client;

public class RefreshService {
    public event Action InstanceIndexRefreshRequested;
    public event Action PlayerRefreshRequested;

    public void CallInstanceIndexRefresh() {
        InstanceIndexRefreshRequested?.Invoke();
    }

    public void CallPlayerRefreshRequested() {
        PlayerRefreshRequested?.Invoke();
    }
}