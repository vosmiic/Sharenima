using System.Security.Claims;
using MatBlazor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using NuGet.Packaging;
using Sharenima.Server.Data;
using Sharenima.Server.Helpers;
using Sharenima.Server.Models;
using Sharenima.Server.SignalR;
using Sharenima.Shared;
using Sharenima.Shared.Queue;
using Xabe.FFmpeg;
using File = Sharenima.Shared.File;
using Instance = Sharenima.Shared.Instance;
using Queue = Sharenima.Shared.Queue.Queue;

namespace Sharenima.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class QueueController : ControllerBase {
    private readonly HttpClient _httpClient;
    private readonly IDbContextFactory<GeneralDbContext> _contextFactory;
    private readonly IHubContext<QueueHub> _hubContext;
    private readonly ILogger<QueueController> _logger;
    private readonly IConfiguration _configuration;

    public QueueController(HttpClient httpClient,
        IDbContextFactory<GeneralDbContext> contextFactory,
        IHubContext<QueueHub> hubContext,
        ILogger<QueueController> logger, IConfiguration configuration) {
        _httpClient = httpClient;
        _contextFactory = contextFactory;
        _hubContext = hubContext;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPost]
    [Authorize(Policy = "AddVideo")]
    public async Task<ActionResult> AddVideoToInstance(Guid instanceId, string videoUrl) {
        YoutubeVideo? youtubeVideoInfo = await OnlineVideoHelpers.GetYoutubeVideoInfo(_httpClient, videoUrl);
        if (youtubeVideoInfo == null) return BadRequest("Provided video URL is incorrect");

        await using var context = await _contextFactory.CreateDbContextAsync();
        Instance? instance = await context.Instances.Where(instance => instance.Id == instanceId).Include(p => p.VideoQueue).FirstOrDefaultAsync();
        if (instance == null) return NotFound("Instance not found");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return BadRequest("Authenticated user could not be found.");

        Queue queue = new Queue {
            AddedById = Guid.Parse(userId),
            Name = youtubeVideoInfo.Title,
            Url = videoUrl,
            Thumbnail = youtubeVideoInfo.ThumbnailUrl,
            VideoType = VideoType.YouTube,
            Order = context.Queues.Count(queue => queue.InstanceId == instanceId)
        };

        instance.VideoQueue.Add(queue);

        await context.SaveChangesAsync();

        _logger.LogInformation($"Video {queue.Name} added to instance {instance.Id} queue");
        await _hubContext.Clients.Group(instanceId.ToString()).SendAsync("AnnounceVideo", instance.VideoQueue);

        return Ok();
    }

    [HttpPost]
    [Authorize(Policy = "UploadVideo")]
    [RequestFormLimits(MultipartBodyLengthLimit = Int64.MaxValue)]
    [RequestSizeLimit(Int64.MaxValue)]
    [Route("fileUpload")]
    public async Task<ActionResult> UploadVideoToInstance(Guid instanceId, string connectionId) {
        List<string> partialErrors = new List<string>();
        IFormFile? fileData = Request.Form.Files[0];
        if (fileData == null) return NoContent();
        await using var context = await _contextFactory.CreateDbContextAsync();
        Instance? instance = await context.Instances.FirstOrDefaultAsync(instance => instance.Id == instanceId);
        if (instance == null)
            return BadRequest("Instance not found");

        string? downloadLocation = context.Settings.FirstOrDefault(setting => setting.Key == SettingKey.DownloadLocation)?.Value;
        //todo read above from appsettings
        if (downloadLocation == null) return BadRequest("User has not setup file uploads");
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
            Name = fileData.FileName,
            VideoType = VideoType.FileUpload,
            MediaType = fileData.ContentType,
            Order = context.Queues.Count(queue => queue.InstanceId == instanceId)
        };

        int lastIndexOfExtension = fileData.FileName.LastIndexOf(".", StringComparison.CurrentCulture);
        string extension = fileData.FileName.Substring(lastIndexOfExtension, fileData.FileName.Length - lastIndexOfExtension);

        string mediaDownloadLocation = Path.Combine(downloadDirectory.FullName, $"temp-{queue.Id.ToString()}{extension}");
        await using FileStream fs = new FileStream(mediaDownloadLocation, FileMode.Create);
        await fileData.CopyToAsync(fs);
        _logger.LogInformation("Created temporary video upload file");
        FileHelper fileHelper = new FileHelper();
        var mediaInfo = await FfmpegHelper.GetFileMetadata(mediaDownloadLocation);
        List<QueueSubtitles> queueSubtitles = new List<QueueSubtitles>();
        if (Request.Form.TryGetValue(nameof(UploadAdvancedSettings.KeepSubtitles), out StringValues stringValues) &&
            Boolean.TryParse(stringValues.First(), out bool keepSubtitles) &&
            keepSubtitles) {
            foreach (ISubtitleStream mediaInfoSubtitleStream in mediaInfo.SubtitleStreams) {
                (string? outputPath, string? error) extraction = await FfmpegHelper.ExtractStreamToFile(_logger, mediaInfoSubtitleStream, Path.Combine(downloadDirectory.FullName, $"subtitles/{queue.Id}/", $"{mediaInfoSubtitleStream.Title}{(string.IsNullOrEmpty(mediaInfoSubtitleStream.Language) ? String.Empty : $" - {mediaInfoSubtitleStream.Language}")}.srt"));
                if (extraction.error != null && !partialErrors.Contains(extraction.error))
                    partialErrors.Add(extraction.error);

                if (extraction.outputPath != null) {
                    queueSubtitles.Add(new QueueSubtitles {
                        FileLocation = Path.Combine(hostedLocation, "subtitles", queue.Id.ToString(), Path.GetFileName(extraction.outputPath)),
                        QueueId = queue.Id,
                        Label = $"{mediaInfoSubtitleStream.Title}{(string.IsNullOrEmpty(mediaInfoSubtitleStream.Language) ? String.Empty : $" - {mediaInfoSubtitleStream.Language}")}"
                    });
                }
            }
        }

        if (mediaInfo.AudioStreams.FirstOrDefault()?.Bitrate == null &&
            mediaInfo.VideoStreams.FirstOrDefault()?.Bitrate == null)
            return BadRequest("Invalid file type");
        FileHelper.SupportedContainer? container = fileHelper.GetMediaContainer(mediaDownloadLocation);
        IVideoStream? firstVideoStream = mediaInfo.VideoStreams.FirstOrDefault();
        IAudioStream? firstAudioStream = mediaInfo.AudioStreams.FirstOrDefault();
        if (container == null ||
            (firstVideoStream != null && Enum.TryParse<VideoCodec>(firstVideoStream?.Codec, false, out VideoCodec videoCodec) &&
             !FileHelper.CheckSupportedFile(container.Value, videoCodec: videoCodec)) ||
            (firstAudioStream != null && Enum.TryParse<AudioCodec>(firstAudioStream?.Codec, false, out AudioCodec audioCodec) &&
             !FileHelper.CheckSupportedFile(container.Value, audioCodec: audioCodec))) {
            //todo need to check settings to know what format to convert to
            _logger.LogInformation("Uploaded file incorrect format; converting to valid web format");
            await _hubContext.Clients.Client(connectionId).SendAsync("ToasterError", "Upload", "File requires converting, this may take some time...", MatToastType.Warning);
            ConvertUploadedFile(firstVideoStream != null, mediaDownloadLocation, downloadDirectory, queue, hostedLocation, fileHelper, instanceId, connectionId, queueSubtitles);

            return Ok();
        }

        string newFileName = Path.Combine(downloadDirectory.FullName, $"{queue.Id.ToString()}{extension}");
        System.IO.File.Move(mediaDownloadLocation, newFileName);
        mediaDownloadLocation = newFileName;


        await AddUploadedVideoToDb(queue, hostedLocation, extension, fileHelper, mediaDownloadLocation, downloadDirectory, context, instanceId, firstVideoStream != null, queueSubtitles);
        await _hubContext.Clients.Client(connectionId).SendAsync("ToasterError", "Uploaded File", "File Uploaded", MatToastType.Info);

        return Ok();
    }

    private async Task ConvertUploadedFile(bool forVideo, string mediaDownloadLocation, DirectoryInfo downloadDirectory, Queue queue, string hostedLocation, FileHelper fileHelper, Guid instanceId, string connectionId, List<QueueSubtitles> queueSubtitles) {
        bool converted;
        if (forVideo) {
            converted = await FfmpegHelper.ConvertVideo(_logger, mediaDownloadLocation, Path.Combine(downloadDirectory.FullName, $"{queue.Id.ToString()}.webm"), videoCodec: VideoCodec.vp9);
        } else {
            converted = await FfmpegHelper.ConvertAudio(_logger, mediaDownloadLocation, Path.Combine(downloadDirectory.FullName, $"{queue.Id.ToString()}.flac"), audioCodec: AudioCodec.flac);
        }

        if (converted) {
            _logger.LogInformation("Successfully converted media");
            queue.MediaType = forVideo ? "video/webm" : "audio/flac";
            await using var context = await _contextFactory.CreateDbContextAsync();
            await AddUploadedVideoToDb(queue, hostedLocation, forVideo ? ".webm" : ".flac", fileHelper, mediaDownloadLocation, downloadDirectory, context, instanceId, forVideo, queueSubtitles);
            await _hubContext.Clients.Client(connectionId).SendAsync("ToasterError", "Uploaded File", "File Uploaded", MatToastType.Info);
        } else {
            _logger.LogInformation("Error converting media");
            await _hubContext.Clients.Client(connectionId).SendAsync("ToasterError", "Error Uploading Media", "Could not upload the media; could not be converted to appropriate web media format", MatToastType.Danger);
        }
    }

    private async Task AddUploadedVideoToDb(Queue queue, string hostedLocation, string extension, FileHelper fileHelper, string videoDownloadLocation, DirectoryInfo downloadDirectory, GeneralDbContext context, Guid instanceId, bool forVideo, List<QueueSubtitles> queueSubtitles) {
        queue.Url = Path.Combine(hostedLocation, $"{queue.Id.ToString()}{extension}");
        if (forVideo) {
            string? thumbnailFileName = await fileHelper.GetVideoThumbnail(videoDownloadLocation, downloadDirectory.FullName, queue.Id.ToString());
            queue.Thumbnail = thumbnailFileName != null ? Path.Combine(hostedLocation, thumbnailFileName) : null;
        }

        context.Queues.Add(queue);
        await context.SaveChangesAsync();

        var subtitleQueue = context.Queues.Where(q => q.Id == queue.Id).Include(q => q.Subtitles).First();
        foreach (QueueSubtitles queueSubtitle in queueSubtitles) {
            subtitleQueue.Subtitles.Add(queueSubtitle);
        }

        await context.SaveChangesAsync();

        _logger.LogInformation($"Video {queue.Name} added to instance {instanceId} queue");
        queue.Subtitles = queue.Subtitles.Select(item => new QueueSubtitles { FileLocation = item.FileLocation }).ToList();
        // fetch an updated list here because file uploads may have taken a long time and queue may have been modified during the process
        var updatedInstanceQueues = await context.Instances.Where(instance => instance.Id == instanceId).Include(p => p.VideoQueue).ThenInclude(q => q.Subtitles).FirstOrDefaultAsync();
        await _hubContext.Clients.Group(instanceId.ToString()).SendAsync("AnnounceVideo", updatedInstanceQueues);
    }

    [HttpGet]
    public async Task<ActionResult> GetInstanceQueue(Guid instanceId) {
        await using var context = await _contextFactory.CreateDbContextAsync();
        Instance? instance = await context.Instances.Where(instance => instance.Id == instanceId).Include(p => p.VideoQueue).ThenInclude(q => q.Subtitles).FirstOrDefaultAsync();
        if (instance == null) return BadRequest("Instance not found");
        return Ok(instance.VideoQueue.ToList());
    }

    [HttpDelete]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> DeleteVideoFromQueue(Guid instanceId, Guid queueId) {
        await using var context = await _contextFactory.CreateDbContextAsync();
        Queue? queue = await context.Queues.FirstOrDefaultAsync(queue => queue.Id == queueId);
        if (queue == null) return NotFound("Queue does not exist");
        List<Queue> instanceQueues = context.Queues.Where(dbQueue => dbQueue.InstanceId == instanceId).ToList();
        foreach (Queue existingQueue in instanceQueues.Where(dbQueue => dbQueue.Order > queue.Order)) {
            existingQueue.Order--;
        }

        context.Remove(queue);
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development") {
            if (queue.VideoType == VideoType.FileUpload) {
                FileHelper.DeleteFile(queue.Url, _configuration, _logger);
                if (queue.Thumbnail != null)
                    FileHelper.DeleteFile(queue.Thumbnail, _configuration, _logger);
            }
        }

        await context.SaveChangesAsync();
        _logger.LogInformation($"Video {queue.Name} removed from instance queue");

        instanceQueues.Remove(queue);
        await _hubContext.Clients.Group(instanceId.ToString()).SendAsync("RemoveVideo", instanceQueues);
        return Ok();
    }

    [HttpPost]
    [Route("order")]
    public async Task<ActionResult> ChangeInstanceQueuesOrder(Guid instanceId, [FromBody] List<Queue> queueList) {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var dbQueueList = context.Queues.Where(queue => queue.InstanceId == instanceId);
        foreach (Queue queue in dbQueueList) {
            Queue? receivedQueue = queueList.FirstOrDefault(item => item.Id == queue.Id);
            if (receivedQueue != null && queueList.IndexOf(receivedQueue) != queue.Order) {
                if (queue.Order == 0) return Forbid();
                queue.Order = queueList.IndexOf(receivedQueue);
            }
        }

        await context.SaveChangesAsync();
        await _hubContext.Clients.Group(instanceId.ToString()).SendAsync("QueueOrderChange", dbQueueList.ToList());
        return Ok();
    }
}