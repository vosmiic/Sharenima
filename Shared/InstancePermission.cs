using System.ComponentModel.DataAnnotations.Schema;

namespace Sharenima.Shared;

public class InstancePermission : Base {
    [ForeignKey("Instance")] public Guid InstanceId { get; set; }
    public Permissions.Permission Permissions { get; set; }
}