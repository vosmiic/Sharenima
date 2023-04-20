using System.ComponentModel.DataAnnotations.Schema;

namespace Sharenima.Shared.Queue;

public class Queue : Base {
    [ForeignKey("Instance")] public Guid InstanceId { get; set; }
    public Guid AddedById { get; set; }
    public VideoType VideoType { get; set; }
    public string Url { get; set; }
    public string Name { get; set; }
    public string? Thumbnail { get; set; }
    public string? MediaType { get; set; }
    public int Order { get; set; }
    private ICollection<QueueSubtitles> _subtitles;
    public virtual ICollection<QueueSubtitles> Subtitles {
        get => _subtitles ?? (_subtitles = new List<QueueSubtitles>());
        set => _subtitles = value;
    }
}

public enum VideoType {
    YouTube,
    FileUpload
}