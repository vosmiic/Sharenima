using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Data;
using Sharenima.Server.Models;
using Stream = Sharenima.Shared.Stream;

namespace Sharenima.Server.Helpers;

public class StreamHelper {
    public static async Task<Stream?> GetStreamDetails(IConfiguration configuration, ILogger logger, string? userId, ApplicationDbContext context, bool createIfNotExist = false) {
        string? baseStreamUrl = configuration["Stream:BaseStreamUrl"];
        string? basePlayUrl = configuration["Stream:BasePlayUrl"];
        string? streamKey = configuration["Stream:Key"];
        if (baseStreamUrl == null || !baseStreamUrl.Contains("{User}")) {
            logger.LogError("Stream incorrectly setup; BaseStreamUrl either missing or {{User}} is missing");
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

        if (user == null) return null;
        Stream? newlyGeneratedStreamDetails = null;
        if (createIfNotExist && user.StreamKey == null) {
            newlyGeneratedStreamDetails = await GenerateStreamDetails(baseStreamUrl, user.UserName, configuration["Stream:Key"]);
            newlyGeneratedStreamDetails.PlayUrl = basePlayUrl.Replace("{User}", user?.UserName);
            return newlyGeneratedStreamDetails;
        }

        string refinedServerUrl = baseStreamUrl.Replace("{User}", "");
        if (refinedServerUrl.EndsWith("/"))
            refinedServerUrl = refinedServerUrl.Remove(refinedServerUrl.Length - 1, 1);
        return new Shared.Stream {
            StreamServer = refinedServerUrl,
            StreamKey = user?.StreamKey,
            PlayUrl = basePlayUrl.Replace("{User}", user?.UserName)
        };
    }

    public static async Task<Stream> GenerateStreamDetails(string baseStreamUrl, string username, string key) {
        string hashedStringUrl = baseStreamUrl.Replace("{User}", username);
        string toBeHashedUrl = $"{hashedStringUrl}?policy={GetEncodedPolicy()}";
        HMACSHA1 hmacsha1 = new HMACSHA1(Encoding.UTF8.GetBytes(key).ToArray()); // old way "abc123"u8.ToArray()
        byte[] hash = hmacsha1.ComputeHash(new ASCIIEncoding().GetBytes(toBeHashedUrl));

        string streamKey = $"{username}?policy={GetEncodedPolicy()}&signature={Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";

        string refinedServerUrl = baseStreamUrl.Replace("{User}", "");
        if (refinedServerUrl.EndsWith("/"))
            refinedServerUrl = refinedServerUrl.Remove(refinedServerUrl.Length - 1, 1);

        return new Stream {
            StreamKey = streamKey,
            StreamServer = refinedServerUrl
        };
    }

    public static string GetEncodedPolicy(Int64 expiresInMilliseconds = 15780000000) {
        string content = JsonSerializer.Serialize(new { url_expire = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 15780000000 });
        byte[] byteContent = System.Text.Encoding.UTF8.GetBytes(content);
        return Convert.ToBase64String(byteContent).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}