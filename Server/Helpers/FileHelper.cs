using FFMpegCore.Enums;
using Sharenima.Server.Models;

namespace Sharenima.Server.Helpers; 

public class FileHelper {
    public FileHelper() {
        //todo need to confirm ffmpeg and ffprobe are installed
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
    public Container? GetMediaContainer(string mediaFilePath) {
        int fileTypeIndex = mediaFilePath.LastIndexOf(".", StringComparison.CurrentCulture);
        if (fileTypeIndex != -1 && Enum.TryParse<Container>(mediaFilePath.Substring(fileTypeIndex + 1), true, out Container container)) {
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

    /// <summary>
    /// Checks if the file can be played in a browser.
    /// </summary>
    /// <param name="container">The container for the file.</param>
    /// <param name="videoCodec">The video codec of the file. Null if no video stream.</param>
    /// <param name="audioCodec">The audio codec of the file. Null if no audio stream.</param>
    /// <returns>True if the file is supported and can be played in a browser.</returns>
    public static bool CheckSupportedFile(Container container, VideoCodecNames? videoCodec = null, AudioCodecNames? audioCodec = null) {
        switch (container) {
            case Container.Mp4:
                if (videoCodec != null)
                    return videoCodec is VideoCodecNames.Av1 or VideoCodecNames.H264 or VideoCodecNames.Vp8 or VideoCodecNames.Vp9;
                if (audioCodec != null)
                    return audioCodec is AudioCodecNames.Aac or AudioCodecNames.Alac or AudioCodecNames.Flac or AudioCodecNames.Mp3 or AudioCodecNames.Opus;
                break;
            case Container.Webm:
                if (videoCodec != null)
                    return videoCodec is VideoCodecNames.Av1 or VideoCodecNames.H264 or VideoCodecNames.Vp8 or VideoCodecNames.Vp9;
                if (audioCodec != null)
                    return audioCodec is AudioCodecNames.Opus or AudioCodecNames.Vorbis;
                break;
            case Container.Ogg:
                if (videoCodec != null)
                    return true;
                if (audioCodec != null)
                    return audioCodec is AudioCodecNames.Flac or AudioCodecNames.Opus or AudioCodecNames.Vorbis;
                break;
            case Container.Mp3:
                if (videoCodec != null)
                    return false;
                if (audioCodec != null)
                    return audioCodec is AudioCodecNames.Mp3;
                break;
            case Container.Flac:
                if (videoCodec != null)
                    return false;
                if (audioCodec != null)
                    return audioCodec is AudioCodecNames.Flac;
                break;
            case Container.Unknown:
                return false;
        }

        return false;
    }

    public static readonly string TemporaryFile = Path.Combine(Path.GetTempPath(), "sharenima", Path.GetRandomFileName());

    public static string RemoveIllegalFileNameCharactersFromString(string input) =>
        string.Join("_", input.Split(Path.GetInvalidFileNameChars()));
    
    public enum Container {
        Mp4,
        Webm,
        Ogg,
        Mp3,
        Flac,
        Unknown
    }
    
    public enum VideoCodecNames {
        Av1,
        H264,
        Vp8,
        Vp9,
        Unknown
    }
    
    public enum AudioCodecNames {
        Aac,
        Alac,
        Flac,
        Mp3,
        Opus,
        Vorbis,
        Unknown
    }
    
    public enum ImageOutputFormat {
        Jpg,
        Png
    }
}