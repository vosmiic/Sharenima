using System.Text.Json;
using Sharenima.Server.Models;

namespace Sharenima.Server.Helpers;

public class FfmpegHelper {
    public static async Task<FfprobeMetadata.Metadata?> GetMetadata(string fileLocation) {
        (string result, bool success, string errorReason) response = await FfmpegCore.RunFfprobeCommand($"-v quiet -print_format json -show_format -show_streams \"{fileLocation}\"");
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
    /// <returns>True if successfully created the subtitles file.</returns>
    public static async Task<bool> ExtractSubtitles(string inputFileLocation, string outputFileLocation, int? subtitleStreamToExtract = null) {
        (Stream stream, bool success, string errorReason) response = await FfmpegCore.RunFfmpegCommand($"-i \"{inputFileLocation}\" -c copy -map 0:{(subtitleStreamToExtract != null ? subtitleStreamToExtract : "s")} -map 0:t? \"{outputFileLocation}\"");
        return response.success || !File.Exists(outputFileLocation);
    }

    /// <summary>
    /// Extract a single subtitle stream from a media file.
    /// </summary>
    /// <param name="inputFileLocation">The file location of the media file to extract the subtitle of.</param>
    /// <param name="outputFileLocation">The file location to save the subtitle file to.</param>
    /// <param name="subtitleStreamToExtract">The subtitle stream to extract.</param>
    /// <returns>True if successfully created the subtitle file.</returns>
    public static async Task<bool> ExtractSubtitle(string inputFileLocation, string outputFileLocation, int subtitleStreamToExtract) {
        (Stream stream, bool success, string errorReason) response = await FfmpegCore.RunFfmpegCommand($"-i \"{inputFileLocation}\" -c copy -map 0:{subtitleStreamToExtract} -codec:s srt \"{outputFileLocation}\"");
        return response.success || !File.Exists(outputFileLocation);
    }

    /// <summary>
    /// Burn subtitles into video.
    /// </summary>
    /// <param name="inputVideoFileLocation">The file location of the media file to burn the subtitles into.</param>
    /// <param name="inputSubtitlesFileLocation">The file location of the subtitles file to burn into the video file.</param>
    /// <param name="outputFileLocation">The file location to save the video file with burnt in subtitles.</param>
    /// <param name="subtitleStreamToBurn">Optional subtitle stream to burn. Defaults to 0.</param>
    /// <returns>True if successfully created the video file with burnt in subtitles.</returns>
    public static async Task<bool> BurnSubtitles(string inputVideoFileLocation, string inputSubtitlesFileLocation, string outputFileLocation, int subtitleStreamToBurn = 0) {
        (Stream stream, bool success, string errorReason) response = await FfmpegCore.RunFfmpegCommand($"-i \"{inputVideoFileLocation}\" -map 0:v -map 0:a -c:v libsvtav1 -crf 35 -b:v 0 -sn -vf \"subtitles='{inputSubtitlesFileLocation}':stream_index={subtitleStreamToBurn}\" \"{outputFileLocation}\"");
        return response.success || !File.Exists(outputFileLocation);
    }
}