using Sharenima.Shared;

namespace Sharenima.Client;

public class QueuePlayerService
{
    public List<Queue> CurrentQueue { get; private set; } = new();
    public void AddToQueue(Queue queue) {
        
        CurrentQueue.Add(queue);
    }

    public void RemoveFromQueue(Queue queue) {
        CurrentQueue.Remove(queue);
    }

    public void SetQueue(List<Queue> queues) {
        CurrentQueue = queues;
    }
}