using System.Diagnostics;

namespace Sharenima.Server.Helpers; 

public class FfmpegCore {
        public static async Task<(string result, bool success, string errorReason)> RunFfprobeCommand(string argument) =>
        await FfprobeCommand(argument);
    public static async Task<(string result, bool success, string errorReason)> RunFfprobeCommand(string argument, string input, FfmpegFormat inputFormat) =>
        await FfprobeCommand(argument, input: input, inputFormat: inputFormat);

    private static async Task<(string result, bool success, string errorReason)> FfprobeCommand(string argument, string? input = null, FfmpegFormat? inputFormat = null) {
        string errorResult = String.Empty;

        Process proc = await RunConsoleCommand($"{(input != null ? $"{input} | " : String.Empty)}ffprobe {argument}");
        string result = await proc.StandardOutput.ReadToEndAsync();
        errorResult += await proc.StandardError.ReadToEndAsync();

        await proc.WaitForExitAsync();

        return (result, errorResult != String.Empty, errorResult);
    }

    public static async Task<(Stream stream, bool success, string errorReason)> RunFfmpegCommand(string argument, FfmpegFormat ffmpegFormat, string? input = null) {
        string errorResult = String.Empty;

        Process proc = await RunConsoleCommand($"{(input != null ? $"\"{input}\" | " : String.Empty)}ffmpeg {argument} -f {ffmpegFormat.ToString().ToLower()} pipe:");
        var memoryStream = proc.StandardOutput.BaseStream;
        errorResult += await proc.StandardError.ReadToEndAsync();

        await proc.WaitForExitAsync();

        return (memoryStream, errorResult != String.Empty, errorResult);
    }

    private static async Task<Process> RunConsoleCommand(string argument) {
        Process proc = new Process();
        if (OperatingSystem.IsWindows()) {
            proc.StartInfo.FileName = "cmd.exe";
            proc.StartInfo.Arguments = argument;
        } else {
            proc.StartInfo.FileName = "/bin/bash";
            proc.StartInfo.Arguments = $"-c \" {argument}\"";
        }

        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.RedirectStandardError = true;
        proc.Start();

        return proc;
    }

    public enum FfmpegFormat {
        Webm
    }
}