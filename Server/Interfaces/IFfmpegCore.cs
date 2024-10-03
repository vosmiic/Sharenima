using System.Diagnostics;

namespace Sharenima.Server.Helpers;

public interface IFfmpegCore {
    /// <summary>
    /// Run an FFprobe command.
    /// </summary>
    /// <param name="argument">Argument to add to the command.</param>
    /// <returns>Execution result.</returns>
    Task<string?> RunFfprobeCommand(string argument);
    /// <summary>
    /// Run an FFprobe command.
    /// </summary>
    /// <param name="argument">Argument to add to the command.</param>
    /// <param name="input">Input string.</param>
    /// <param name="inputFormat">Input format of the input media.</param>
    /// <returns>Execution result.</returns>
    Task<string?> RunFfprobeCommand(string argument, string input, FfmpegCore.FfmpegFormat inputFormat);
    /// <summary>
    /// Run an FFmpeg command.
    /// </summary>
    /// <param name="argument">Argument to add to the command.</param>
    /// <returns>Generated stream, true if the command ran to completion, error reason if failed; null if successful.</returns>
    Task<MemoryStream?> RunFfmpegCommand(string argument);

    /// <summary>
    /// Run an FFmpeg command.
    /// </summary>
    /// <param name="argument">Argument to add to the command.</param>
    /// <param name="ffmpegFormat">Output format.</param>
    /// <param name="output">Output location.</param>
    /// <returns>Generated stream.</returns>
    Task<MemoryStream?> RunFfmpegCommand(string argument, FfmpegCore.FfmpegFormat ffmpegFormat, string? output = null);

    /// <summary>
    /// Run an FFmpeg command
    /// </summary>
    /// <param name="argument">Argument to add to the command.</param>
    /// <param name="input">Input string.</param>
    /// <param name="ffmpegFormat">Output format.</param>
    /// <param name="output">Output location.</param>
    /// <returns>Generated stream.</returns>
    Task<MemoryStream?> RunFfmpegCommand(string argument, string input, FfmpegCore.FfmpegFormat ffmpegFormat, string? output = null);

    /// <summary>
    /// Run an FFmpeg command
    /// </summary>
    /// <param name="argument">Arguments to add to the command.</param>
    /// <param name="input">Input string.</param>
    /// <param name="ffmpegFormat">Output format.</param>
    /// <param name="output">Output location.</param>
    /// <returns>Generated stream.</returns>
    Task<MemoryStream?> RunFfmpegCommand(List<string> argument, string input, FfmpegCore.FfmpegFormat ffmpegFormat, string? output = null);
    
    /// <summary>
    /// Run an FFmpeg command
    /// </summary>
    /// <param name="argument">Arguments to add to the command.</param>
    /// <param name="input">Input string.</param>
    /// <param name="ffmpegFormat">Output format.</param>
    /// <param name="output">Output location.</param>
    /// <returns>Generated stream.</returns>
    Task<MemoryStream?> RunFfmpegCommand(List<string> argument, FileInfo input, FfmpegCore.FfmpegFormat ffmpegFormat, string? output = null);
}