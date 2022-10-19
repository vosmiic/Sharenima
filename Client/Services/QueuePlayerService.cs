using Sharenima.Shared;

namespace Sharenima.Client;

public class QueuePlayerService
{
    public Queue? CurrentQueueVideo { get; private set; }
    public event Action RefreshRequested;
    public event Action ChangeVideo;

    public void SetCurrentQueueVideo(Queue queue) {
        CurrentQueueVideo = queue;
        CallChangeVideo();
    }
    public void CallRequestRefresh()
    {
        RefreshRequested?.Invoke();
    }

    public void CallChangeVideo() {
        ChangeVideo?.Invoke();
    }
}