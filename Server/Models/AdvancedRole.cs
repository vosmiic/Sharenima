using Sharenima.Shared;

namespace Sharenima.Server.Models; 

public class AdvancedRole : Base {
    public virtual ApplicationUser User { get; set; }
    public string UserId { get; set; }
    public Guid InstanceId { get; set; }
    public Permission Permission { get; set; }
}

public enum Permission {
    Administrator
}