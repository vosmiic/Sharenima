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
    /// <param name="mediaFilePath">Path to the video.</param>
    /// <returns>Container and codec of the video.</returns>
    public SupportedContainer? GetMediaContainer(string mediaFilePath) {
        int fileTypeIndex = mediaFilePath.LastIndexOf(".", StringComparison.CurrentCulture);
        if (fileTypeIndex != -1 && Enum.TryParse<SupportedContainer>(mediaFilePath.Substring(fileTypeIndex + 1), true, out SupportedContainer container)) {
            return container;
        }

        return null;
    }

    public static void DeleteFile(string fileUrl, IConfiguration configuration, ILogger logger) {
        string? rootVideoDownloadLocation = configuration["VideoDownloadLocation"];
        if (rootVideoDownloadLocation == null) return;
        string videoFileAndDirectory = fileUrl.Replace("/files/", String.Empty);
        string fileLocation = Path.Combine(rootVideoDownloadLocation, videoFileAndDirectory);
        
        if (File.Exists(fileLocation)) {
            logger.LogInformation($"Attempting to delete uploaded file {fileLocation}...");
            try {
                File.Delete(fileLocation);
            } catch (Exception e) {
                logger.LogWarning($"Could not delete uploaded file {fileLocation}; {e.Message}");
            }
        } else {
            logger.LogWarning($"Could not delete uploaded file {fileLocation}; file does not exist or incorrect permissions");
        }
    }

    public static bool CheckSupportedFile(SupportedContainer container, VideoCodec? videoCodec = null, AudioCodec? audioCodec = null) {
        switch (container) {
            case SupportedContainer.Mp4:
                if (videoCodec != null)
                    return videoCodec is VideoCodec.av1 or VideoCodec.h264 or VideoCodec.vp8 or VideoCodec.vp9;
                if (audioCodec != null)
                    return audioCodec is AudioCodec.aac or AudioCodec.alac or AudioCodec.flac or AudioCodec.mp3 or AudioCodec.opus;
                break;
            case SupportedContainer.Webm:
                if (videoCodec != null)
                    return videoCodec is VideoCodec.av1 or VideoCodec.h264 or VideoCodec.vp8 or VideoCodec.vp9;
                if (audioCodec != null)
                    return audioCodec is AudioCodec.opus or AudioCodec.vorbis;
                break;
            case SupportedContainer.Ogg:
                if (videoCodec != null)
                    return true;
                if (audioCodec != null)
                    return audioCodec is AudioCodec.flac or AudioCodec.opus or AudioCodec.vorbis;
                break;
            case SupportedContainer.Mp3:
                if (videoCodec != null)
                    return false;
                if (audioCodec != null)
                    return audioCodec is AudioCodec.mp3;
                break;
            case SupportedContainer.Flac:
                if (videoCodec != null)
                    return false;
                if (audioCodec != null)
                    return audioCodec is AudioCodec.flac;
                break;
            default:
                return false;
        }

        return false;
    }

    public enum SupportedContainer {
        Mp4,
        Webm,
        Ogg,
        Mp3,
        Flac
    }
}