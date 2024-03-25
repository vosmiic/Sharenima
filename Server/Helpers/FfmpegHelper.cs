using System.Text.Json;
using Sharenima.Server.Models;

namespace Sharenima.Server.Helpers;

public class FfmpegHelper {
    public static async Task<FfprobeMetadata.Metadata?> GetMetadata(string fileLocation) {
        (string result, bool success, string errorReason) response = await FfmpegCore.RunFfprobeCommand($"ffprobe -v quiet -print_format json -show_format -show_streams \"{fileLocation}\"");
        if (!response.success) return null;
        FfprobeMetadata.Metadata? metadata = JsonSerializer.Deserialize<FfprobeMetadata.Metadata>(response.result);
        return metadata;
    }

    public static void GetMetadata(MemoryStream memoryStream) {
        
    }
    
    
}