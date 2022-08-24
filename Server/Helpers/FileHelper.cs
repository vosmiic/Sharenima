using Xabe.FFmpeg.Downloader;

namespace Sharenima.Server.Helpers; 

public class FileHelper {
    public static async Task<string?> GetVideoThumbnail(string filePath, string folderPath, string fileName) {
        if (await FfmpegHelper.IsFfmpegInstalled("ffmpeg -version") && await FfmpegHelper.IsFfmpegInstalled("ffprobe -version"))
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);

        string? duration = await FfmpegHelper.FfmpegCommand(false, $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 {filePath}");
        if (duration == null) return null; //todo log here
        var thumbnailRandomPoint = new Random().Next(0, Convert.ToInt32(Convert.ToDouble(duration.Replace("\n", ""))));
        var createThumbnailTask = await FfmpegHelper.FfmpegCommand(true, $"-i {filePath} -vf \"scale=150:150:force_original_aspect_ratio=decrease\" -ss {thumbnailRandomPoint} -vframes 1 {Path.Combine(folderPath, fileName)}.jpg");
        return createThumbnailTask != null ? $"{Path.Combine(folderPath, fileName)}.jpg" : null;
    }
}