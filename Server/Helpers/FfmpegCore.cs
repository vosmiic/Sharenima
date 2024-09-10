using System.Text;
using CliWrap;
using CliWrap.Exceptions;

namespace Sharenima.Server.Helpers;

public class FfmpegCore : IFfmpegCore {
    public ILogger Logger { get; set; }
    
    public FfmpegCore(ILogger logger) {
        Logger = logger;
    }
    
    /// <inheritdoc />
    public async Task<string?> RunFfprobeCommand(string argument) =>
        await FfprobeCommand(argument);

    /// <inheritdoc />
    public async Task<string?> RunFfprobeCommand(string argument, string input, FfmpegFormat inputFormat) =>
        await FfprobeCommand(argument, input: input, inputFormat: inputFormat);
    
    private async Task<string?> FfprobeCommand(string argument, string? input = null, FfmpegFormat? inputFormat = null) {
        MemoryStream? result = await RunConsoleCommand("ffprobe", new List<string> { argument }, input, inputFormat);
        if (result == null) return null;
        using var streamReader = new StreamReader(result);
        streamReader.BaseStream.Seek(0, SeekOrigin.Begin);
        string stringResult = await streamReader.ReadToEndAsync();
        return stringResult;
    }

    /// <inheritdoc />
    public async Task<MemoryStream?> RunFfmpegCommand(string argument) =>
        await FfmpegCommand(argument);
    
    /// <inheritdoc />
    public async Task<MemoryStream?> RunFfmpegCommand(string argument, FfmpegFormat ffmpegFormat, string? output = null) =>
        await FfmpegCommand(argument, null, ffmpegFormat, output);
    
    /// <inheritdoc />
    public async Task<MemoryStream?> RunFfmpegCommand(string argument, string input, FfmpegFormat ffmpegFormat, string? output = null) =>
        await FfmpegCommand(argument, input, ffmpegFormat, output);

    private async Task<MemoryStream?> FfmpegCommand(string argument, string? input = null, FfmpegFormat? ffmpegFormat = null, string? output = null) =>
        await RunConsoleCommand("ffmpeg",
            new List<string> {
                "-y",
                argument
            },
            input,
            ffmpegFormat,
            output);

    private async Task<MemoryStream?> RunConsoleCommand(string commandTarget, List<string> arguments, string? input = null, FfmpegFormat? ffmpegFormat = null, string? output = null) {
        StringBuilder errorResult = new StringBuilder();

        var command = Cli.Wrap(commandTarget)
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(errorResult));
        if (input != null) {
            command = command.WithStandardInputPipe(PipeSource.FromString(input));
        }

        List<string> commandArguments = new List<string>();
        commandArguments.AddRange(arguments);

        if (ffmpegFormat != null) {
            commandArguments.Add($"-f {ffmpegFormat.Value.ToString().ToLower()}");
        }

        if (output != null) {
            commandArguments.Add(output == "pipe:" ? output : $"-o {output}");
        }

        MemoryStream memoryStream = new MemoryStream();
        command = command.WithArguments(commandArguments, false) | PipeTarget.ToStream(memoryStream);

        try {
            await command.ExecuteAsync();
        } catch (CommandExecutionException e) {
            Logger.LogError($"{commandTarget} command failed; reason: \n {e.Message} \n Command output: \n {errorResult}");
            throw;
        }

        return memoryStream;
    }

    public enum FfmpegFormat {
        Webm,
        Image2Pipe
    }
}