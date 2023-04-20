using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Sharenima.Shared.Queue; 

public class QueueSubtitles : Base {
    [JsonIgnore]
    public virtual Queue Queue { get; set; }
    public Guid QueueId { get; set; }
    public string FileLocation { get; set; }
    public string Label { get; set; }
}