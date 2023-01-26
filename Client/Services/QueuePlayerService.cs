using Sharenima.Shared;

namespace Sharenima.Client;

public class QueuePlayerService
{
    public List<Queue> CurrentQueue { get; private set; } = new();
    public void AddToQueue(Queue queue) {
        CurrentQueue.Add(queue);
    }

    public void RemoveFromQueue(Queue removedQueue) {
        CurrentQueue.Remove(removedQueue);
        CurrentQueue.Where(queue => queue.Order > removedQueue.Order).ToList().ForEach(queue => queue.Order--);
    }

    public void SetQueue(List<Queue> queues) {
        CurrentQueue = queues;
    }
}