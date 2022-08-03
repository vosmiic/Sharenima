using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sharenima.Client.ComponentCode;
using Sharenima.Server.Data;
using Sharenima.Server.Helpers;
using Sharenima.Server.Models;
using Instance = Sharenima.Shared.Instance;
using Queue = Sharenima.Shared.Queue;

namespace Sharenima.Server.Controllers; 

[ApiController]
[Route("[controller]")]
public class QueueController : ControllerBase {
    private readonly HttpClient _httpClient;
    private readonly IDbContextFactory<GeneralDbContext> _contextFactory;

    public QueueController(HttpClient httpClient, IDbContextFactory<GeneralDbContext> contextFactory) {
        _httpClient = httpClient;
        _contextFactory = contextFactory;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult> AddVideoToInstance(Guid instanceId, string videoUrl) {
        YoutubeVideo? youtubeVideoInfo = await OnlineVideoHelpers.GetYoutubeVideoInfo(_httpClient, videoUrl);
        if (youtubeVideoInfo == null) return BadRequest("Provided video URL is incorrect");
        
        await using var context = await _contextFactory.CreateDbContextAsync();
        Instance? instance = await context.Instances.FirstOrDefaultAsync(instance => instance.Id == instanceId);
        if (instance == null) return NotFound("Instance not found");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return BadRequest("Authenticated user could not be found.");

        Queue queue = new Queue {
            AddedById = Guid.Parse(userId),
            Name = youtubeVideoInfo.Title,
            Url = videoUrl
        };
        
        instance.VideoQueue.Add(queue);

        await context.SaveChangesAsync();
        return Ok();
    }
}