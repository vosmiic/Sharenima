using Xabe.FFmpeg;

namespace Sharenima.Server.Helpers;

public class FfmpegHelper {
    public static async Task<string?> FfmpegCommand(bool ffmpegCommand, string command) {
        if (ffmpegCommand) {
            IConversionResult? ffmpegResult = await FFmpeg.Conversions.New().AddParameter(command).Start();
            return ffmpegResult.Arguments;
        }

        string? ffprobeResult = await Probe.New().Start(command);
        return ffprobeResult;
    }

    public static async Task<bool> ConvertVideo(ILogger logger, string videoFilePath, string convertedVideoOutputPath, VideoCodec videoCodec) => await ConvertMedia(logger, videoFilePath, convertedVideoOutputPath, videoCodec: videoCodec);
    public static async Task<bool> ConvertAudio(ILogger logger, string audioFilePath, string convertedAudioOutputPath, AudioCodec audioCodec) => await ConvertMedia(logger, audioFilePath, convertedAudioOutputPath, audioCodec: audioCodec);

    private static async Task<bool> ConvertMedia(ILogger logger, string mediaFilePath, string convertedMediaOutputPath, VideoCodec? videoCodec = null, AudioCodec? audioCodec = null) {
        IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(mediaFilePath);

        IStream? stream = null;
        if (videoCodec != null) {
            stream = mediaInfo.VideoStreams.FirstOrDefault()?.SetCodec(videoCodec.Value);
        }

        if (audioCodec != null) {
            stream = mediaInfo.AudioStreams.FirstOrDefault()?.SetCodec(audioCodec.Value);
        }

        if (stream == null) {
            logger.LogInformation("Cannot convert video; no media streams detected");
            return false;
        }

        IConversion conversion = FFmpeg.Conversions.New()
            .AddStream(stream)
            .SetOutput(convertedMediaOutputPath);
        if (videoCodec != null) {
            IStream? audioStream = mediaInfo.AudioStreams.FirstOrDefault();
            if (audioStream != null)
                conversion.AddStream(audioStream);
        }

        try {
            await conversion.Start();
        } catch (Exception e) {
            // todo log e
            logger.LogError($"Error converting media; {e.Message}");
            return false;
        }

        return true;
    }

    public static async Task<(string? outputPath, string? error)> ExtractStreamToFile(ILogger logger, ISubtitleStream subtitleStream, string outputPath) {
        // checking if file exists; appending counter to name if so
        if (File.Exists(outputPath)) {
            int counter = 1;
            while (true) {
                string newName = outputPath.Replace(".srt", $" ({counter}).srt");
                if (File.Exists(newName)) {
                    counter++;
                } else {
                    outputPath = newName;
                    break;
                }
            }
        }
        
        IConversion conversion = FFmpeg.Conversions.New()
            .AddStream(subtitleStream)
            .SetOutput(outputPath);

        try {
            await conversion.Start();
        } catch (Exception e) {
            logger.LogError($"Error converting media; {e.Message}");
            return (null, e.Message);
        }
        
        return (outputPath, null);
    }

    public static async Task<IMediaInfo> GetFileMetadata(string filePath) {
        return await FFmpeg.GetMediaInfo(filePath);
    }


    public static async Task<bool> IsFfmpegInstalled(string argument) {
        string errorResult = "";
        using System.Diagnostics.Process proc = new System.Diagnostics.Process();
        if (OperatingSystem.IsWindows()) {
            proc.StartInfo.FileName = "cmd.exe";
            proc.StartInfo.Arguments = argument;
        } else {
            proc.StartInfo.FileName = "/bin/bash";
            proc.StartInfo.Arguments = $"-c \" {argument} \"";
        }

        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.RedirectStandardError = true;
        proc.Start();

        errorResult += await proc.StandardError.ReadToEndAsync();

        await proc.WaitForExitAsync();

        return string.IsNullOrEmpty(errorResult);
    }
}