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
        var instanceQueue = CurrentQueue.Where(queue => queue.Order > removedQueue.Order).ToList();
        foreach (Queue queue in instanceQueue) {
            Console.WriteLine(queue.Name);
            queue.Order--;
        }
        SetQueue(CurrentQueue.Union(instanceQueue).ToList());
    }

    public Queue? GetNextInQueue() => CurrentQueue.FirstOrDefault(queue => queue.Order == 1);

    public Queue? GetCurrentQueue() => CurrentQueue.MinBy(queue => queue.Order);

    public void SetQueue(List<Queue> queues) {
        CurrentQueue = queues.OrderBy(queue => queue.Order).ToList();
    }
}