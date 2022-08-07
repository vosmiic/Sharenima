using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sharenima.Client.ComponentCode;
using Sharenima.Server.Data;
using Sharenima.Server.Helpers;
using Sharenima.Server.Models;
using Sharenima.Server.SignalR;
using Instance = Sharenima.Shared.Instance;
using Queue = Sharenima.Shared.Queue;

namespace Sharenima.Server.Controllers; 

[ApiController]
[Route("[controller]")]
public class QueueController : ControllerBase {
    private readonly HttpClient _httpClient;
    private readonly IDbContextFactory<GeneralDbContext> _contextFactory;
    private readonly IHubContext<QueueHub> _hubContext;

    public QueueController(HttpClient httpClient,
        IDbContextFactory<GeneralDbContext> contextFactory,
        IHubContext<QueueHub> hubContext) {
        _httpClient = httpClient;
        _contextFactory = contextFactory;
        _hubContext = hubContext;
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
            Url = videoUrl,
            Thumbnail = youtubeVideoInfo.ThumbnailUrl
        };
        
        instance.VideoQueue.Add(queue);

        await context.SaveChangesAsync();

        await _hubContext.Clients.Group(instanceId.ToString()).SendAsync("AnnounceVideo", queue);
        
        return Ok();
    }

    [HttpGet]
    public async Task<ActionResult> GetInstanceQueue(Guid instanceId) {
        await using var context = await _contextFactory.CreateDbContextAsync();
        Instance? instance = await context.Instances.Where(instance => instance.Id == instanceId).Include(p => p.VideoQueue).FirstOrDefaultAsync();
        if (instance == null) return BadRequest("Instance not found");
        return Ok(instance.VideoQueue.ToList());
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteVideoFromQueue(Guid instanceId, Guid queueId) {
        await using var context = await _contextFactory.CreateDbContextAsync();
        Queue? queue = await context.Queues.FirstOrDefaultAsync(queue => queue.Id == queueId);
        if (queue == null) return NotFound("Queue does not exist");
        context.Remove(queue);
        await context.SaveChangesAsync();
        await _hubContext.Clients.Group(instanceId.ToString()).SendAsync("RemoveVideo", queue.Id);
        return Ok();
    }
}