using System.Security.Claims;
using MatBlazor;
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
    private readonly ILogger<QueueController> _logger;

    public QueueController(HttpClient httpClient,
        IDbContextFactory<GeneralDbContext> contextFactory,
        IHubContext<QueueHub> hubContext,
        ILogger<QueueController> logger) {
        _httpClient = httpClient;
        _contextFactory = contextFactory;
        _hubContext = hubContext;
        _logger = logger;
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
        
        _logger.LogInformation($"Video {queue.Name} added to instance {instance.Id} queue");
        await _hubContext.Clients.Group(instanceId.ToString()).SendAsync("AnnounceVideo", queue);

        return Ok();
    }

    [HttpPost]
    [Authorize(Policy = "UploadVideo")]
    [Route("fileUpload")]
    public async Task<ActionResult> UploadVideoToInstance(Guid instanceId, string connectionId, [FromBody] File fileData) {
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
            _logger.LogInformation($"Creating directory {downloadDirectory.FullName}");
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
        _logger.LogInformation("Created temporary video upload file");
        FileHelper fileHelper = new FileHelper();
        var mediaInfo = await FfmpegHelper.GetFileMetadata(videoDownloadLocation);
        if (mediaInfo.VideoStreams.FirstOrDefault()?.Bitrate == null) return BadRequest("Invalid file type");
        FileHelper.SupportedContainer? container = fileHelper.GetVideoContainer(videoDownloadLocation);
        IVideoStream? firstVideoStream = mediaInfo.VideoStreams.FirstOrDefault();
        if (container == null ||
            (Enum.TryParse<VideoCodec>(firstVideoStream?.Codec, false, out VideoCodec videoCodec) &&
            !FileHelper.CheckSupportedFile(videoCodec, container.Value))) {
            //todo need to check settings to know what format to convert to
            _logger.LogInformation("Video incorrect format; converting to valid web format");
            ConvertVideo(videoDownloadLocation, downloadDirectory, queue, hostedLocation, fileHelper, instanceId, connectionId);

            return Ok();
        }

        string newFileName = Path.Combine(downloadDirectory.FullName, $"{queue.Id.ToString()}{extension}");
        System.IO.File.Move(videoDownloadLocation, newFileName);
        videoDownloadLocation = newFileName;


        await AddUploadedVideoToDb(queue, hostedLocation, extension, fileHelper, videoDownloadLocation, downloadDirectory, context, instanceId);

        return Ok();
    }

    private async Task ConvertVideo(string videoDownloadLocation, DirectoryInfo downloadDirectory, Queue queue, string hostedLocation, FileHelper fileHelper, Guid instanceId, string connectionId) {
        bool converted = await FfmpegHelper.ConvertVideo(_logger, VideoCodec.vp9, videoDownloadLocation, Path.Combine(downloadDirectory.FullName, $"{queue.Id.ToString()}.webm"));
        
        if (converted) {
            _logger.LogInformation("Successfully converted video");
            queue.MediaType = "video/webm";
            await using var context = await _contextFactory.CreateDbContextAsync();
            await AddUploadedVideoToDb(queue, hostedLocation, ".webm", fileHelper, videoDownloadLocation, downloadDirectory, context, instanceId);
        } else {
            _logger.LogInformation("Error converting video");
            await _hubContext.Clients.Client(connectionId).SendAsync("ToasterError", "Error Uploading Video", "Could not upload the video; could not be converted to appropriate web video format", MatToastType.Danger);
        }
    }

    private async Task AddUploadedVideoToDb(Queue queue, string hostedLocation, string extension, FileHelper fileHelper, string videoDownloadLocation, DirectoryInfo downloadDirectory, GeneralDbContext context, Guid instanceId) {
        queue.Url = Path.Combine(hostedLocation, $"{queue.Id.ToString()}{extension}");
        string? thumbnailFileName = await fileHelper.GetVideoThumbnail(videoDownloadLocation, downloadDirectory.FullName, queue.Id.ToString());
        queue.Thumbnail = thumbnailFileName != null ? Path.Combine(hostedLocation, thumbnailFileName) : null;

        context.Queues.Add(queue);
        await context.SaveChangesAsync();
        
        _logger.LogInformation($"Video {queue.Name} added to instance {instanceId} queue");
        await _hubContext.Clients.Group(instanceId.ToString()).SendAsync("AnnounceVideo", queue);
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
        _logger.LogInformation($"Video {queue.Name} removed from instance queue");
        await _hubContext.Clients.Group(instanceId.ToString()).SendAsync("RemoveVideo", queue.Id);
        return Ok();
    }
}