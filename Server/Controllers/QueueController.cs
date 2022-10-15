using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Data;
using Sharenima.Server.Helpers;
using Sharenima.Server.Models;
using Sharenima.Server.SignalR;
using Sharenima.Shared;
using Xabe.FFmpeg;
using File = Sharenima.Shared.File;
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
    [Authorize(Policy = "AddVideo")]
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
            Thumbnail = youtubeVideoInfo.ThumbnailUrl,
            VideoType = VideoType.YouTube
        };

        instance.VideoQueue.Add(queue);

        await context.SaveChangesAsync();

        await _hubContext.Clients.Group(instanceId.ToString()).SendAsync("AnnounceVideo", queue);

        return Ok();
    }

    [HttpPost]
    [Authorize(Policy = "UploadVideo")]
    [Route("fileUpload")]
    public async Task<ActionResult> UploadVideoToInstance(Guid instanceId, [FromBody] File fileData) {
        await using var context = await _contextFactory.CreateDbContextAsync();
        Instance? instance = await context.Instances.FirstOrDefaultAsync(instance => instance.Id == instanceId);
        if (instance == null)
            return BadRequest("Instance not found");

        string? downloadLocation = context.Settings.FirstOrDefault(setting => setting.Key == SettingKey.DownloadLocation)?.Value;
        if (downloadLocation == null) return BadRequest("User has not setup file uploads");
        byte[] bytes = Convert.FromBase64String(fileData.fileBase64);
        DirectoryInfo downloadDirectory = new DirectoryInfo(Path.Combine(downloadLocation, instance.Name));
        string hostedLocation = Path.Combine("/files", instance.Name);
        if (!downloadDirectory.Exists) {
            Directory.CreateDirectory(downloadDirectory.FullName);
        }

        Queue queue = new Queue {
            Id = Guid.NewGuid(),
            AddedById = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)),
            InstanceId = instance.Id,
            Name = fileData.fileName,
            VideoType = VideoType.FileUpload,
            MediaType = fileData.MediaType
        };

        int lastIndexOfExtension = fileData.fileName.LastIndexOf(".", StringComparison.CurrentCulture);
        string extension = fileData.fileName.Substring(lastIndexOfExtension, fileData.fileName.Length - lastIndexOfExtension);

        string videoDownloadLocation = Path.Combine(downloadDirectory.FullName, $"temp-{queue.Id.ToString()}{extension}");
        await System.IO.File.WriteAllBytesAsync(videoDownloadLocation, bytes);
        
        FileHelper fileHelper = new FileHelper();
        (FileHelper.SupportedContainer? container, VideoCodec? codec) containerCodec = await fileHelper.GetVideoContainerCodec(videoDownloadLocation);
        if (containerCodec.container == null ||
            containerCodec.codec == null ||
            (containerCodec.container != null && 
             containerCodec.codec != null && 
             !FileHelper.CheckSupportedFile(containerCodec.codec.Value, containerCodec.container.Value))) {
            //todo need to check settings to know what format to convert to
            await FfmpegHelper.ConvertVideo(VideoCodec.vp9, videoDownloadLocation, Path.Combine(downloadDirectory.FullName, $"{queue.Id.ToString()}.webm"));
        } else {
            string newFileName = Path.Combine(downloadDirectory.FullName, $"{queue.Id.ToString()}{extension}");
            System.IO.File.Move(videoDownloadLocation, newFileName);
            videoDownloadLocation = newFileName;
            // remove temp tag
        }
        
        queue.Url = Path.Combine(hostedLocation, $"{queue.Id.ToString()}{extension}");
        string? thumbnailFileName = await fileHelper.GetVideoThumbnail(videoDownloadLocation, downloadDirectory.FullName, queue.Id.ToString());
        queue.Thumbnail = thumbnailFileName != null ? Path.Combine(hostedLocation, thumbnailFileName) : null;

        context.Queues.Add(queue);
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
    [Authorize(Policy = "Admin")]
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