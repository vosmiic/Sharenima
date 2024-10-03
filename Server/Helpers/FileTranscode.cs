using CliWrap;
using Sharenima.Server.Models;

namespace Sharenima.Server.Helpers;

public class FileTranscode {
    public IFfmpegCore FfmpegCore { get; set; }
    private List<string> _transcodeArguments = new ();
    private FileInfo InputFile { get; set; }
    private string AudioMapCommand(int streamId) => $"-c:a:{streamId}";
    private string VideoMapCommand(int streamId) => $"-c:v:{streamId}";
    private FileHelper.AudioCodecNames AudioEncodeCodec => FileHelper.AudioCodecNames.Aac; // todo should be user configurable
    private FileHelper.VideoCodecNames VideoEncodeCodec => FileHelper.VideoCodecNames.Vp9; // todo should be user configurable

    public FileTranscode(IFfmpegCore ffmpegCore, FileInfo inputFile) {
        FfmpegCore = ffmpegCore;
        InputFile = inputFile;
    }

    /// <summary>
    /// Add a stream to the transcode arguments.
    /// </summary>
    /// <param name="stream">Stream to add to the argument list.</param>
    public void AddStream(FfprobeMetadata.Stream stream) {
        switch (stream.CodecType) {
            case FfprobeMetadata.CodecType.Audio:
                _transcodeArguments.Add($"{AudioMapCommand(stream.Index)} {AudioEncodeCodec}".ToLower());
                break;
            case FfprobeMetadata.CodecType.Video:
                _transcodeArguments.Add($"{VideoMapCommand(stream.Index)} {VideoEncodeCodec}".ToLower());
                break;
        }
    }

    /// <summary>
    /// Transcodes the file using the supplied arguments.
    /// </summary>
    /// <param name="output">Output location for the file.</param>
    /// <returns>True if the transcoding was successful.</returns>
    public async Task<bool> Transcode(string output) {
        _transcodeArguments.Insert(0, $"-sn -map 0:v -map 0:a -c copy");
        return await FfmpegCore.RunFfmpegCommand(_transcodeArguments, InputFile, Helpers.FfmpegCore.FfmpegFormat.Mp4, output) != null;
    }
    
    /*
     * Working transcode command: ffmpeg -i hakenawa.mkv -sn -map 0:v -map 0:a -c copy -f webm -c:v libsvtav1 -vf "subtitles='/home/patrick/Desktop/tmp/subtitles.mkv':stream_index=0" hanekawa-converted.webm
     */

    /// <summary>
    /// Extracts subtitles from a file and then queues up the subtitles to be added to the new transcoded file. 
    /// </summary>
    /// <param name="subtitleFileLocation">Path of file to extract the subtitles to.</param>
    /// <param name="subtitleStreamToBurn">Subtitle stream to burn. Default is 0.</param>
    /// <returns>True if the subtitles were extracted and added to the argument list.</returns>
    public async Task<bool> AddExtractSubtitleFile(string subtitleFileLocation, int? subtitleStreamToBurn = null) {
        FfmpegHelper ffmpegHelper = new FfmpegHelper(FfmpegCore);
        bool success = await ffmpegHelper.ExtractSubtitles(InputFile.FullName, subtitleFileLocation, subtitleStreamToBurn);
        if (!success) return false;
        _transcodeArguments.Add($"-vf \"subtitles='{subtitleFileLocation}':stream_index={(subtitleStreamToBurn != null ? subtitleStreamToBurn : "0")}\"");
        return true;
    }
}