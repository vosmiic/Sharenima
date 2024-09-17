using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Sharenima.Server.Helpers; 

public class FileHelper {
    public FileHelper() {
        if (FfmpegHelper.IsFfmpegInstalled("ffmpeg -version").Result && FfmpegHelper.IsFfmpegInstalled("ffprobe -version").Result)
            FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official).Wait();
    }
    public async Task<MemoryStream?> GetVideoThumbnailStream(IFfmpegCore ffmpegCore, FfmpegHelper ffmpegHelper, string filePath, ImageOutputFormat imageFormat) {
        var metadata = await ffmpegHelper.GetMetadata(filePath);
        if (metadata == null) return null; //todo log here
        var thumbnailRandomPoint = new Random().Next(0, Convert.ToInt32(Convert.ToDouble(metadata.Format.Duration.Replace("\n", ""))));
        var createThumbnailTaskStream = await ffmpegCore.RunFfmpegCommand($"-i {filePath} -ss {thumbnailRandomPoint} -s 150x150 -vframes 1 -c:v {imageFormat.ToString().ToLower()}", FfmpegCore.FfmpegFormat.Image2Pipe, "pipe:");
        if (createThumbnailTaskStream == null) return null;

        return createThumbnailTaskStream;
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