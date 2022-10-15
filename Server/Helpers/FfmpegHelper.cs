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

    public static async Task<bool> ConvertVideo(VideoCodec codec, string videoFilePath, string convertedVideoOutputPath) {
        IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(videoFilePath);

        IStream? videoStream = mediaInfo.VideoStreams.FirstOrDefault()?.SetCodec(codec);
        if (videoStream == null) return false;
        IStream? audioStream = mediaInfo.AudioStreams.FirstOrDefault();

        IConversion conversion = FFmpeg.Conversions.New()
            .AddStream(videoStream)
            .SetOutput(convertedVideoOutputPath);
        if (audioStream != null) {
            conversion.AddStream(audioStream);
        }

        try {
            await conversion.Start();
        } catch (Exception e) {
            // todo log e
            return false;
        }

        return true;
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