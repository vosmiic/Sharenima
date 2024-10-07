using System.Text.Json;
using Sharenima.Server.Models;

namespace Sharenima.Server.Helpers;

public class FfmpegHelper {
    private readonly IFfmpegCore _ffmpegCore;

    public FfmpegHelper(IFfmpegCore ffmpegCore) {
        _ffmpegCore = ffmpegCore;
    }

    public async Task<FfprobeMetadata.Metadata?> GetMetadata(string fileLocation) {
        string? response = await _ffmpegCore.RunFfprobeCommand($"-v quiet -print_format json -show_format -show_streams \"{fileLocation}\"");
        if (response == null) return null;
        FfprobeMetadata.Metadata? metadata = JsonSerializer.Deserialize<FfprobeMetadata.Metadata>(response);
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
    public async Task<bool> ExtractSubtitles(string inputFileLocation, string outputFileLocation, int? subtitleStreamToExtract = null) {
        MemoryStream? response = await _ffmpegCore.RunFfmpegCommand($"-i \"{inputFileLocation}\" -c copy -map 0:{(subtitleStreamToExtract != null ? subtitleStreamToExtract : "s")} -map 0:t? \"{outputFileLocation}\"");
        return response != null && File.Exists(outputFileLocation);
    }

    /// <summary>
    /// Extract a single subtitle stream from a media file.
    /// </summary>
    /// <param name="inputFileLocation">The file location of the media file to extract the subtitle of.</param>
    /// <param name="outputFileLocation">The file location to save the subtitle file to.</param>
    /// <param name="subtitleStreamToExtract">The subtitle stream to extract.</param>
    /// <returns>True if successfully created the subtitle file.</returns>
    public async Task<bool> ExtractSubtitle(string inputFileLocation, string outputFileLocation, int subtitleStreamToExtract) {
        MemoryStream? response = await _ffmpegCore.RunFfmpegCommand($"-i \"{inputFileLocation}\" -c copy -map 0:{subtitleStreamToExtract} -codec:s srt \"{outputFileLocation}\"");
        return response != null && File.Exists(outputFileLocation);
    }

    /// <summary>
    /// Burn subtitles into video.
    /// </summary>
    /// <param name="inputVideoFileLocation">The file location of the media file to burn the subtitles into.</param>
    /// <param name="inputSubtitlesFileLocation">The file location of the subtitles file to burn into the video file.</param>
    /// <param name="outputFileLocation">The file location to save the video file with burnt in subtitles.</param>
    /// <param name="subtitleStreamToBurn">Optional subtitle stream to burn. Defaults to 0.</param>
    /// <returns>True if successfully created the video file with burnt in subtitles.</returns>
    public async Task<bool> BurnSubtitles(string inputVideoFileLocation, string inputSubtitlesFileLocation, string outputFileLocation, int subtitleStreamToBurn = 0) {
        MemoryStream? response = await _ffmpegCore.RunFfmpegCommand($"-i \"{inputVideoFileLocation}\" -map 0:v -map 0:a -c:v libsvtav1 -crf 35 -b:v 0 -sn -vf \"subtitles='{inputSubtitlesFileLocation}':stream_index={subtitleStreamToBurn}\" \"{outputFileLocation}\"");
        return response == null || !File.Exists(outputFileLocation);
    }
}