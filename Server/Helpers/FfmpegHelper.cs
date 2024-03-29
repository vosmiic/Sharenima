using System.Text.Json;
using Sharenima.Server.Models;

namespace Sharenima.Server.Helpers;

public class FfmpegHelper {
    public static async Task<FfprobeMetadata.Metadata?> GetMetadata(string fileLocation) {
        (string result, bool success, string errorReason) response = await FfmpegCore.RunFfprobeCommand($"ffprobe -v quiet -print_format json -show_format -show_streams \"{fileLocation}\"");
        if (!response.success) return null;
        FfprobeMetadata.Metadata? metadata = JsonSerializer.Deserialize<FfprobeMetadata.Metadata>(response.result);
        return metadata;
    }

    public static void GetMetadata(MemoryStream memoryStream) {
        
    }

    /// <summary>
    /// Create subtitles from a media file.
    /// </summary>
    /// <param name="inputFileLocation">The file location of the media file to extract the subtitles of.</param>
    /// <param name="outputFileLocation">The file location to save the subtitles file to.</param>
    /// <param name="subtitleStreamToExtract">Optional stream to extract. Will only extract this single stream.</param>
    /// <returns></returns>
    public static async Task<bool> CreateSubtitles(string inputFileLocation, string outputFileLocation, int? subtitleStreamToExtract = null) {
        (Stream stream, bool success, string errorReason) response = await FfmpegCore.RunFfmpegCommand($"-i \"{inputFileLocation}\" -c copy -map 0:{(subtitleStreamToExtract != null ? subtitleStreamToExtract : "s")} -map 0:t? \"{outputFileLocation}\"");
        return response.success || !File.Exists(outputFileLocation);
    }
    
    
}