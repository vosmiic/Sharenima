using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NuGet.Common;
using Sharenima.Server.Data;
using Sharenima.Server.Models;
using Sharenima.Shared;

namespace Sharenima.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class StreamController : ControllerBase {
    private readonly IDbContextFactory<ApplicationDbContext> _applicationDbContextFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StreamController> _logger;

    public StreamController(IDbContextFactory<ApplicationDbContext> applicationDbContextFactory, IConfiguration configuration, ILogger<StreamController> logger) {
        _applicationDbContextFactory = applicationDbContextFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult> Index(string userName) {
        string? baseStreamUrl = _configuration["Stream:BaseStreamUrl"];
        string? basePlayUrl = _configuration["Stream:BasePlayUrl"];
        if (baseStreamUrl == null || !baseStreamUrl.Contains("{User}")) {
            _logger.LogError("Stream incorrectly setup; BaseStreamUrl either missing or {{User}} is missing");
            return StatusCode(500);
        }

        if (basePlayUrl == null || !basePlayUrl.Contains("{User}")) {
            _logger.LogError("Stream incorrectly setup; BasePlayUrl either missing or {{User}} is missing");
            return StatusCode(500);
        }
        await using var context = await _applicationDbContextFactory.CreateDbContextAsync();
        ApplicationUser? user = await context.Users.FirstOrDefaultAsync(user => user.UserName == userName);
        if (user == null) return NotFound();
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        string refinedServerUrl = baseStreamUrl.Replace("{User}", "");
        if (refinedServerUrl.EndsWith("/"))
            refinedServerUrl = refinedServerUrl.Remove(refinedServerUrl.Length - 1, 1);
        return Ok(new Shared.Stream {
            StreamServer = refinedServerUrl,
            StreamKey = userId != null && user.Id == userId ? user.StreamKey : null,
            PlayUrl = basePlayUrl.Replace("{User}", user.UserName)
        });
    }

    [HttpPost]
    [Route("create")]
    public async Task<ActionResult> VerifyStreamAuthentication() {
        string? baseStreamUrl = _configuration["Stream:BaseStreamUrl"];
        string? basePlayUrl = _configuration["Stream:BasePlayUrl"];
        if (baseStreamUrl == null || !baseStreamUrl.Contains("{User}")) {
            _logger.LogError("Stream incorrectly setup; BaseStreamUrl either missing or {{User}} is missing");
            return StatusCode(500);
        }

        if (basePlayUrl == null || !basePlayUrl.Contains("{User}")) {
            _logger.LogError("Stream incorrectly setup; BasePlayUrl either missing or {{User}} is missing");
            return StatusCode(500);
        }

        string content = JsonSerializer.Serialize(new { url_expire = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 15780000000 });
        byte[] byteContent = System.Text.Encoding.UTF8.GetBytes(content);
        string encodedPolicy = Convert.ToBase64String(byteContent).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await using var context = await _applicationDbContextFactory.CreateDbContextAsync();
        ApplicationUser? user = await context.Users.FirstOrDefaultAsync(user => user.Id == userId);
        if (user != null) {
            string hashedStringUrl = baseStreamUrl.Replace("{User}", user.UserName);
            string toBeHashedUrl = $"{hashedStringUrl}?policy={encodedPolicy}";
            HMACSHA1 hmacsha1 = new HMACSHA1("bIw!2iq"u8.ToArray());
            byte[] hash = hmacsha1.ComputeHash(new ASCIIEncoding().GetBytes(toBeHashedUrl));

            user.StreamKey = $"{user.UserName}?policy={encodedPolicy}&signature={Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
            await context.SaveChangesAsync();

            string refinedServerUrl = baseStreamUrl.Replace("{User}", "");
            if (refinedServerUrl.EndsWith("/"))
                refinedServerUrl = refinedServerUrl.Remove(refinedServerUrl.Length - 1, 1);
            return Ok(new Shared.Stream {
                StreamServer = refinedServerUrl,
                StreamKey = $"{user.UserName}?policy={encodedPolicy}&signature={Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_')}"
            });
        }

        return Unauthorized();
    }
}