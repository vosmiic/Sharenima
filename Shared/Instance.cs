using System.Collections.ObjectModel;

namespace Sharenima.Shared;

public class Instance : Base {
    private ICollection<Queue> videoQueues;
    private ICollection<InstancePermission> permissions;
    public string Name { get; set; }
    public Guid CreateById { get; set; }

    public ICollection<Queue> VideoQueue {
        get => videoQueues ?? (videoQueues = new Collection<Queue>());
        protected set => videoQueues = value;
    }

    public ICollection<InstancePermission> Permissions {
        get => permissions ?? (permissions = new Collection<InstancePermission>());
        protected set => permissions = value;
    }

    public TimeSpan VideoTime { get; set; }
    public State? PlayerState { get; set; }
}