using Xabe.FFmpeg;

namespace Sharenima.Server.Helpers; 

public class FfmpegHelper {
    public static async Task<string?> FfmpegCommand(bool ffmpegCommand, string command) {
        if (ffmpegCommand) {
            Console.WriteLine(command);
            IConversionResult? ffmpegResult = await FFmpeg.Conversions.New().AddParameter(command).Start();
            return ffmpegResult.Arguments;
        }

        string? ffprobeResult = await Probe.New().Start(command);
        return ffprobeResult;
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