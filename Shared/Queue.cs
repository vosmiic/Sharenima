using System.ComponentModel.DataAnnotations.Schema;

namespace Sharenima.Shared; 

public class Queue : Base {
    [ForeignKey("Instance")]
    public Guid InstanceId { get; set; }
    public Guid AddedById { get; set; }
    public string Url { get; set; }
    public string Name { get; set; }
}