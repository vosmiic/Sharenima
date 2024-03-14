using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Data;
using Sharenima.Server.Helpers;
using Sharenima.Server.Models;

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
    public async Task<ActionResult> Index(string? userName = null, bool browserStreaming = false) {
        await using var context = await _applicationDbContextFactory.CreateDbContextAsync();
        ApplicationUser? user = null;
        if (userName != null) {
            user = await context.Users.FirstOrDefaultAsync(applicationUser => applicationUser.UserName == userName);
            if (user == null) return NotFound();
        }

        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Shared.Stream? streamDetails = await StreamHelper.GetStreamDetails(_configuration, _logger, user?.Id ?? userId, context, true, browserStreaming);

        if (streamDetails == null) return StatusCode(500);

        if (userName != null && (userId == null || user?.Id != userId))
            streamDetails.StreamKey = null;
        return Ok(streamDetails);
    }

    [HttpPost]
    [Route("create")]
    public async Task<ActionResult> VerifyStreamAuthentication() {
        string? baseStreamUrl = _configuration["Stream:BaseStreamUrl"];
        string? basePlayUrl = _configuration["Stream:BasePlayUrl"];
        string? streamKey = _configuration["Stream:Key"];
        if (baseStreamUrl == null || !baseStreamUrl.Contains("{User}")) {
            _logger.LogError("Stream incorrectly setup; BaseStreamUrl either missing or {{User}} is missing");
            return StatusCode(500);
        }

        if (basePlayUrl == null || !basePlayUrl.Contains("{User}")) {
            _logger.LogError("Stream incorrectly setup; BasePlayUrl either missing or {{User}} is missing");
            return StatusCode(500);
        }

        if (streamKey == null) {
            _logger.LogError("Stream incorrectly setup; Key missing");
            return StatusCode(500);
        }

        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await using var context = await _applicationDbContextFactory.CreateDbContextAsync();
        ApplicationUser? user = await context.Users.FirstOrDefaultAsync(user => user.Id == userId);
        if (user != null) {
            var stream = StreamHelper.GenerateStreamDetails(baseStreamUrl, user.UserName, streamKey);

            user.StreamKey = stream.StreamKey;
            await context.SaveChangesAsync();

            return Ok(stream);
        }

        return Unauthorized();
    }
}