namespace Sharenima.Client;

public class QueuePlayerService
{
    public event Action RefreshRequested;
    public event Action ChangeVideo;
    public void CallRequestRefresh()
    {
        RefreshRequested?.Invoke();
    }

    public void CallChangeVideo() {
        ChangeVideo?.Invoke();
    }
}