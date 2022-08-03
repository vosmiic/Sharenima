using System.Text.Json.Serialization;

namespace Sharenima.Server.Models;

public class YoutubeVideo {
    [JsonPropertyName("title")] public string Title { get; set; }

    [JsonPropertyName("author_name")] public string AuthorName { get; set; }

    [JsonPropertyName("author_url")] public string AuthorUrl { get; set; }

    [JsonPropertyName("type")] public string Type { get; set; }

    [JsonPropertyName("height")] public int Height { get; set; }

    [JsonPropertyName("width")] public int Width { get; set; }

    [JsonPropertyName("version")] public string Version { get; set; }

    [JsonPropertyName("provider_name")] public string ProviderName { get; set; }

    [JsonPropertyName("provider_url")] public string ProviderUrl { get; set; }

    [JsonPropertyName("thumbnail_height")] public int ThumbnailHeight { get; set; }

    [JsonPropertyName("thumbnail_width")] public int ThumbnailWidth { get; set; }

    [JsonPropertyName("thumbnail_url")] public string ThumbnailUrl { get; set; }

    [JsonPropertyName("html")] public string Html { get; set; }
}