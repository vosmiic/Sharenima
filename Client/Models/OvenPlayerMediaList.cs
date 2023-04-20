using System.Text.Json.Serialization;
using Sharenima.Shared.Queue;

namespace Sharenima.Client.Models; 

public class OvenPlayerMediaList {
    [JsonPropertyName("sources")]
    public List<Source> Sources { get; set; }

    [JsonPropertyName("tracks")]
    public List<Track> Tracks { get; set; }

    public static OvenPlayerMediaList GenerateMediaListFromQueue(Queue queue) {
        List<Track> trackList = new List<Track>();
        foreach (QueueSubtitles queueSubtitle in queue.Subtitles) {
            trackList.Add(new Track {
                File = queueSubtitle.FileLocation,
                Kind = "captions",
                Label = queueSubtitle.Label
            });
        }

        return new OvenPlayerMediaList {
            Sources = new List<Source> {
                new Source {
                    File = queue.Url,
                    Type = queue.MediaType ?? String.Empty,
                    Label = queue.Name
                }
            },
            Tracks = trackList
        };
    }
}

public class Source
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("file")]
    public string File { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; }
}

public class Track
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; }

    [JsonPropertyName("file")]
    public string File { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; }
}