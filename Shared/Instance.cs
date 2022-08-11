using System.Collections.ObjectModel;

namespace Sharenima.Shared; 

public class Instance : Base {
    private ICollection<Queue> videoQueues;
    public string Name { get; set; }
    public Guid CreateById { get; set; }
    public ICollection<Queue> VideoQueue {             
        get => videoQueues ?? (videoQueues = new Collection<Queue>());
        protected set => videoQueues = value;
    }
    public TimeSpan VideoTime { get; set; }
}