using Xabe.FFmpeg.Downloader;

namespace Sharenima.Server.Helpers; 

public class FileHelper {
    public FileHelper() {
        if (FfmpegHelper.IsFfmpegInstalled("ffmpeg -version").Result && FfmpegHelper.IsFfmpegInstalled("ffprobe -version").Result)
            FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official).Wait();
    }
    public async Task<string?> GetVideoThumbnail(string filePath, string folderPath, string fileName) {
        string? duration = await FfmpegHelper.FfmpegCommand(false, $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 {filePath}");
        if (string.IsNullOrEmpty(duration)) return null; //todo log here
        var thumbnailRandomPoint = new Random().Next(0, Convert.ToInt32(Convert.ToDouble(duration.Replace("\n", ""))));
        var createThumbnailTask = await FfmpegHelper.FfmpegCommand(true, $"-i {filePath} -vf \"scale=150:150:force_original_aspect_ratio=decrease\" -ss {thumbnailRandomPoint} -vframes 1 {Path.Combine(folderPath, fileName)}.jpg");
        return !string.IsNullOrEmpty(createThumbnailTask) ? $"{fileName}.jpg" : null;
    }

    /// <summary>
    /// Gets the container and codec of a video.
    /// </summary>
    /// <param name="videoFilePath">Path to the video.</param>
    /// <returns>Container and codec of the video.</returns>
    public async Task<(SupportedContainer? container, SupportCodecType? codec)> GetVideoContainerCodec(string videoFilePath) {
        string? codec = await FfmpegHelper.FfmpegCommand(false, $"-v error -select_streams v:0 -show_entries stream=codec_name -of default=noprint_wrappers=1:nokey=1 {videoFilePath}");
        SupportCodecType? convertedCodec = null;
        if (!string.IsNullOrEmpty(codec) && Enum.TryParse(codec, out SupportCodecType outCodec)) {
            convertedCodec = outCodec;
        }
        
        int fileTypeIndex = videoFilePath.LastIndexOf(".", StringComparison.CurrentCulture);
        if (fileTypeIndex != -1 && Enum.TryParse(videoFilePath.Substring(fileTypeIndex), out SupportedContainer container)) {
            return (container, convertedCodec);
        }

        return (null, convertedCodec);
    }

    public static bool CheckSupportedFile(SupportCodecType codec, SupportedContainer container) {
        switch (container) {
            case SupportedContainer.Mp4:
                return codec is SupportCodecType.Av1 or SupportCodecType.Avc or SupportCodecType.Hevc or SupportCodecType.Vp9;
            case SupportedContainer.Webm:
                return codec is SupportCodecType.Av1 or SupportCodecType.Vp8 or SupportCodecType.Vp9;
            default:
                return false;
        }
    }
    
    public enum SupportCodecType {
        Av1,
        Hevc,
        Avc,
        Vp8,
        Vp9
    }
    
    public enum SupportedContainer {
        Mp4,
        Webm
    }
}