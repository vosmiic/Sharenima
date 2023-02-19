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
    private readonly string _baseStreamUrl = "";
    private readonly string _basePlayUrl = "";

    public StreamController(IDbContextFactory<ApplicationDbContext> applicationDbContextFactory) {
        _applicationDbContextFactory = applicationDbContextFactory;
    }

    [HttpGet]
    public async Task<ActionResult> Index(string userName) {
        await using var context = await _applicationDbContextFactory.CreateDbContextAsync();
        ApplicationUser? user = await context.Users.FirstOrDefaultAsync(user => user.UserName == userName);
        if (user == null) return NotFound();
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Ok(new Shared.Stream {
            StreamServer = _baseStreamUrl,
            StreamKey = userId != null && user.Id == userId ? user.StreamKey : null,
            PlayUrl = $"{_basePlayUrl}/{user.UserName}"
        });
    }

    [HttpPost]
    [Route("create")]
    public async Task<ActionResult> VerifyStreamAuthentication() {
        string content = JsonSerializer.Serialize(new { url_expire = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 15780000000 });
        byte[] byteContent = System.Text.Encoding.UTF8.GetBytes(content);
        string encodedPolicy = Convert.ToBase64String(byteContent).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await using var context = await _applicationDbContextFactory.CreateDbContextAsync();
        ApplicationUser? user = await context.Users.FirstOrDefaultAsync(user => user.Id == userId);
        if (user != null) {
            string toBeHashedUrl = $"{_baseStreamUrl}/{user.UserName}?policy={encodedPolicy}";
            HMACSHA1 hmacsha1 = new HMACSHA1("aKq#1kj"u8.ToArray());
            byte[] hash = hmacsha1.ComputeHash(new ASCIIEncoding().GetBytes(toBeHashedUrl));
            string streamUrl = $"{toBeHashedUrl}&signature={Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
            
            user.StreamKey = $"{user.UserName}?policy={encodedPolicy}&signature={Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
            await context.SaveChangesAsync();
            
            return Ok(new Shared.Stream {
                StreamServer = _baseStreamUrl,
                StreamKey = $"{user.UserName}?policy={encodedPolicy}&signature={Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_')}"
            });
        }

        return Unauthorized();
    }
}