using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Data;
using Sharenima.Server.Models;
using Stream = Sharenima.Shared.Stream;

namespace Sharenima.Server.Helpers;

public class StreamHelper {
    public static async Task<Stream?> GetStreamDetails(IConfiguration configuration, ILogger logger, string? userId, ApplicationDbContext context, bool createIfNotExist = false, bool browserStreaming = false) {
        string? baseStreamUrl = configuration["Stream:BaseStreamUrl"];
        string? wssBaseStreamUrl = configuration["Stream:WssBaseStreamUrl"];
        string? basePlayUrl = configuration["Stream:BasePlayUrl"];
        string? streamKey = configuration["Stream:Key"];
        if (!browserStreaming && (baseStreamUrl == null || !baseStreamUrl.Contains("{User}"))) {
            logger.LogError("Stream incorrectly setup; BaseStreamUrl either missing or {{User}} is missing");
            return null;
        }

        if (browserStreaming && (wssBaseStreamUrl == null || !wssBaseStreamUrl.Contains("{User}"))) {
            logger.LogError("Stream incorrectly setup; WssBaseStreamUrl missing");
            return null;
        }

        if (basePlayUrl == null || !basePlayUrl.Contains("{User}")) {
            logger.LogError("Stream incorrectly setup; BasePlayUrl either missing or {{User}} is missing");
            return null;
        }

        if (streamKey == null) {
            logger.LogError("Stream incorrectly setup; Key missing");
            return null;
        }

        ApplicationUser? user = await context.Users.FirstOrDefaultAsync(user => user.Id == userId);
        string playUrl = basePlayUrl.Replace("{User}", user?.UserName);

        if (user == null) return null;
        Stream? newlyGeneratedStreamDetails = null;
        if (browserStreaming) {
            Stream streamDetails = await GenerateStreamDetails(wssBaseStreamUrl.Replace("{User}", user.UserName), user.UserName, streamKey, "direction=send&transport=tcp");
            return new Shared.Stream {
                StreamServer = $"{wssBaseStreamUrl.Replace("{User}", String.Empty)}{streamDetails.StreamKey}&direction=send&transport=tcp",
                StreamKey = user?.StreamKey,
                PlayUrl = playUrl
            };
        }

        var generatedStreamDetails = await GenerateStreamDetails(baseStreamUrl, user.UserName, streamKey);
        generatedStreamDetails.PlayUrl = playUrl;
        return generatedStreamDetails;
    }

    public static async Task<Stream> GenerateStreamDetails(string baseStreamUrl, string username, string key, string? additionalParameters = null) {
        string encodedPolicy = GetEncodedPolicy();
        string hashedStringUrl = baseStreamUrl.Replace("{User}", username);
        string toBeHashedUrl = $"{hashedStringUrl}?policy={encodedPolicy}{(additionalParameters != null ? $"&{additionalParameters}" : String.Empty)}";
        HMACSHA1 hmacsha1 = new HMACSHA1(Encoding.UTF8.GetBytes(key).ToArray());
        byte[] hash = hmacsha1.ComputeHash(new ASCIIEncoding().GetBytes(toBeHashedUrl));
        
        string refinedServerUrl = baseStreamUrl.Replace("{User}", "");
        if (refinedServerUrl.EndsWith("/"))
            refinedServerUrl = refinedServerUrl.Remove(refinedServerUrl.Length - 1, 1);

        return new Stream {
            StreamKey = $"{username}?policy={encodedPolicy}&signature={Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_')}",
            StreamServer = refinedServerUrl
        };
    }

    public static string GetEncodedPolicy(Int64 expiresInMilliseconds = 15780000000) {
        string content = JsonSerializer.Serialize(new { url_expire = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 15780000000 });
        byte[] byteContent = System.Text.Encoding.UTF8.GetBytes(content);
        return Convert.ToBase64String(byteContent).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}