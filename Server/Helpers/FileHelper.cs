using Xabe.FFmpeg;
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
    public SupportedContainer? GetVideoContainer(string videoFilePath) {
        int fileTypeIndex = videoFilePath.LastIndexOf(".", StringComparison.CurrentCulture);
        if (fileTypeIndex != -1 && Enum.TryParse<SupportedContainer>(videoFilePath.Substring(fileTypeIndex + 1), true, out SupportedContainer container)) {
            return container;
        }

        return null;
    }

    public static bool CheckSupportedFile(VideoCodec codec, SupportedContainer container) {
        switch (container) {
            case SupportedContainer.Mp4:
            case SupportedContainer.Webm:
                return codec is VideoCodec.av1 or VideoCodec.h264 or VideoCodec.vp8 or VideoCodec.vp9;
            case SupportedContainer.Ogg:
                return true;
            default:
                return false;
        }
    }

    public enum SupportedContainer {
        Mp4,
        Webm,
        Ogg
    }
}