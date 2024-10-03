using System.Security.Claims;
using System.Text.Json;
using MatBlazor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Data;
using Sharenima.Server.Helpers;
using Sharenima.Server.Models;
using Sharenima.Server.Services;
using Sharenima.Server.SignalR;
using Sharenima.Shared;
using Sharenima.Shared.Queue;
using StackExchange.Redis;
using Instance = Sharenima.Shared.Instance;
using Queue = Sharenima.Shared.Queue.Queue;

namespace Sharenima.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class QueueController : ControllerBase {
    private readonly HttpClient _httpClient;
    private readonly IDbContextFactory<GeneralDbContext> _contextFactory;
    private readonly IHubContext<QueueHub> _hubContext;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<QueueController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public QueueController(HttpClient httpClient, IDbContextFactory<GeneralDbContext> contextFactory,
        IHubContext<QueueHub> hubContext, IConfiguration configuration, IConnectionMultiplexer connectionMultiplexer,
        ILoggerFactory loggerFactory) {
        _httpClient = httpClient;
        _contextFactory = contextFactory;
        _hubContext = hubContext;
        _logger = loggerFactory.CreateLogger<QueueController>();
        _configuration = configuration;
        _connectionMultiplexer = connectionMultiplexer;
        _loggerFactory = loggerFactory;
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

        Queue queue = new Queue {
            AddedById = userId != null ? Guid.Parse(userId) : null,
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
    public async Task<ActionResult> UploadVideoToInstance(Guid instanceId, string connectionId, bool burnSubtitle, int chosenSubtitleStream) {
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

        string? user = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Queue queue = new Queue {
            Id = Guid.NewGuid(),
            AddedById = user != null ? Guid.Parse(user) : null,
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
        FfmpegCore ffmpegCore = new FfmpegCore(_logger);
        FfmpegHelper ffmpegHelper = new FfmpegHelper(ffmpegCore);
        FileTranscode fileTranscode = new FileTranscode(ffmpegCore, new FileInfo(mediaDownloadLocation));
        FfprobeMetadata.Metadata? mediaInfo = await ffmpegHelper.GetMetadata(mediaDownloadLocation);
        if (mediaInfo == null) {
            return BadRequest("Could not get metadata from file");
        }

        string finalMediaLocation = Path.Combine(downloadDirectory.FullName, instanceId.ToString(), $"{queue.Id}.mp4");
        List<QueueSubtitles> subtitles = new List<QueueSubtitles>();

        if (burnSubtitle) {
            string subtitlesFileLocation = $"{FileHelper.TemporaryFile}.mkv";
            bool subtitlesExtracted = await fileTranscode.AddExtractSubtitleFile(subtitlesFileLocation, chosenSubtitleStream);
            if (!subtitlesExtracted) return BadRequest("Could not extract subtitles from the file");
        } else {
            subtitles = new List<QueueSubtitles>();
            DirectoryInfo mediaDirectory = Directory.CreateDirectory(Path.Combine(downloadDirectory.FullName, instanceId.ToString()));
            foreach (FfprobeMetadata.Stream subtitleStream in mediaInfo.Streams.Where(stream => stream.CodecType == FfprobeMetadata.CodecType.Subtitle)) {
                string subtitleSaveLocation = Path.Combine(mediaDirectory.FullName, $"{FileHelper.RemoveIllegalFileNameCharactersFromString(subtitleStream.Tags.Title)}.srt");
                bool extractedSubtitle = await ffmpegHelper.ExtractSubtitle(mediaDownloadLocation, subtitleSaveLocation, subtitleStream.Index);
                if (extractedSubtitle) {
                    subtitles.Add(new QueueSubtitles {
                        QueueId = queue.Id,
                        Label = subtitleStream.Tags.Title,
                        FileLocation = Path.Combine("/files", instance.Name, mediaDirectory.Name, $"{FileHelper.RemoveIllegalFileNameCharactersFromString(subtitleStream.Tags.Title)}.srt")
                    });
                } else {
                    _logger.LogWarning($"Could not extract subtitle {subtitleStream.Tags.Title} on queue {queue.Id}.");
                }
            }
        }

        if (burnSubtitle) {
            // Transcoding is required, may as well let ffmpeg convert the incompatible streams itself
            await fileTranscode.Transcode(finalMediaLocation);
        } else {
            List<int> incompatibleStreams = GetIncompatibleStreams(mediaInfo);

            if (incompatibleStreams.Count > 0) {
                foreach (int streamId in incompatibleStreams) {
                    fileTranscode.AddStream(mediaInfo.Streams.First(stream => stream.Index == streamId));
                }

                bool successful = await fileTranscode.Transcode(finalMediaLocation);
                if (!successful) return BadRequest("Could not transcode video");
            }
        }


        string newFileName = Path.Combine(downloadDirectory.FullName, $"{queue.Id.ToString()}{extension}");
        System.IO.File.Move(mediaDownloadLocation, newFileName);
        mediaDownloadLocation = newFileName;

        await AddUploadedVideoToDb(ffmpegCore, ffmpegHelper, queue, hostedLocation, extension, fileHelper, mediaDownloadLocation, downloadDirectory, context, instanceId, mediaInfo.Streams.Any(stream => stream.CodecType == FfprobeMetadata.CodecType.Video), subtitles);
        await _hubContext.Clients.Client(connectionId).SendAsync("ToasterError", "Uploaded File", "File Uploaded", MatToastType.Info);

        return Ok();
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

    [HttpPost]
    [Route("statechange")]
    [Authorize(Policy = "ChangeProgress")]
    public async Task<ActionResult> ChangeInstanceState(Guid instanceId, State playerState, Guid queueId) {
        IDatabase redisDatabase = _connectionMultiplexer.GetDatabase();
        string redisStateChangeLockKey = RedisHelper.InstanceStateChangeKey(instanceId);
        if (redisDatabase.KeyExists(redisStateChangeLockKey)) {
            return StatusCode(500, JsonSerializer.Serialize(new ErrorResponse {
                Reason = "Too many state change requests at once! Please try again.",
            }));
        }

        redisDatabase.StringSet(redisStateChangeLockKey, true);
        await using var context = await _contextFactory.CreateDbContextAsync();

        Queue? queue = context.Queues.FirstOrDefault(queue => queue.Id == queueId);
        if (queue != null) {
            Instance? instance = await context.Instances.FirstOrDefaultAsync(instance => instance.Id == queue.InstanceId);
            if (playerState == State.Ended) {
                context.Remove(queue);
                SortQueueOrder(context, queue.InstanceId);
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development")
                    if (queue.VideoType == VideoType.FileUpload) {
                        FileHelper.DeleteFile(queue.Url, _configuration, _logger);
                        if (queue.Thumbnail != null)
                            FileHelper.DeleteFile(queue.Thumbnail, _configuration, _logger);
                    }

                if (instance != null) {
                    InstanceTimeTracker instanceTimeTracker = new InstanceTimeTracker(_loggerFactory, _connectionMultiplexer);
                    instanceTimeTracker.Remove(instance.Id);
                }
            }

            if (instance != null) {
                _logger.LogInformation($"Updating instance {instance.Id} state in database");
                instance.PlayerState = playerState;
            }

            await context.SaveChangesAsync();
            await _hubContext.Clients.Group(instanceId.ToString()).SendAsync("ReceiveStateChange", playerState);
        }

        redisDatabase.KeyDelete(redisStateChangeLockKey);

        return new OkResult();
    }

    private void SortQueueOrder(GeneralDbContext generalDbContext, Guid instanceId) {
        var queues = generalDbContext.Queues.Where(queue => queue.InstanceId == instanceId).AsEnumerable().Where(queue => generalDbContext.Entry(queue).State != EntityState.Deleted).OrderBy(queue => queue.Order).ToList();

        for (int i = 0; i < queues.Count(); i++) {
            if (i == 0) {
                queues[i].Order = 0;
                continue;
            }

            int previousOrder = queues[i - 1].Order;
            queues[i].Order = previousOrder + 1;
        }
    }

    private async Task ConvertUploadedFile(bool forVideo, string mediaDownloadLocation, DirectoryInfo downloadDirectory, Queue queue, string hostedLocation, FileHelper fileHelper, Guid instanceId, string connectionId, List<QueueSubtitles> queueSubtitles) {
        bool converted;
        // if (forVideo) {
        //     converted = await FfmpegHelper.ConvertVideo(_logger, mediaDownloadLocation, Path.Combine(downloadDirectory.FullName, $"{queue.Id.ToString()}.webm"), videoCodec: VideoCodec.vp9);
        // } else {
        //     converted = await FfmpegHelper.ConvertAudio(_logger, mediaDownloadLocation, Path.Combine(downloadDirectory.FullName, $"{queue.Id.ToString()}.flac"), audioCodec: AudioCodec.flac);
        // }

        // if (converted) {
        //     _logger.LogInformation("Successfully converted media");
        //     queue.MediaType = forVideo ? "video/webm" : "audio/flac";
        //     await using var context = await _contextFactory.CreateDbContextAsync();
        //     await AddUploadedVideoToDb(queue, hostedLocation, forVideo ? ".webm" : ".flac", fileHelper, mediaDownloadLocation, downloadDirectory, context, instanceId, forVideo, queueSubtitles);
        //     await _hubContext.Clients.Client(connectionId).SendAsync("ToasterError", "Uploaded File", "File Uploaded", MatToastType.Info);
        // } else {
        //     _logger.LogInformation("Error converting media");
        //     await _hubContext.Clients.Client(connectionId).SendAsync("ToasterError", "Error Uploading Media", "Could not upload the media; could not be converted to appropriate web media format", MatToastType.Danger);
        // }
    }

    private async Task AddUploadedVideoToDb(IFfmpegCore ffmpegCore, FfmpegHelper ffmpegHelper, Queue queue, string hostedLocation, string extension, FileHelper fileHelper, string videoDownloadLocation, DirectoryInfo downloadDirectory, GeneralDbContext context, Guid instanceId, bool forVideo, List<QueueSubtitles> queueSubtitles) {
        queue.Url = Path.Combine(hostedLocation, $"{queue.Id.ToString()}{extension}");
        FileHelper.ImageOutputFormat imageOutputFormat = FileHelper.ImageOutputFormat.Png;
        var fileName = $"{queue.Id.ToString()}.{imageOutputFormat.ToString().ToLower()}";
        if (forVideo) {
            MemoryStream? thumbnailFileName = await fileHelper.GetVideoThumbnailStream(ffmpegCore, ffmpegHelper, videoDownloadLocation, imageOutputFormat);
            if (thumbnailFileName != null) {
                var thumbnailFileStream = System.IO.File.Open(Path.Combine(downloadDirectory.FullName, fileName), FileMode.Create);
                thumbnailFileName.WriteTo(thumbnailFileStream);
                thumbnailFileStream.Close();
            }

            queue.Thumbnail = thumbnailFileName != null ? Path.Combine(hostedLocation, fileName) : null;
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
        await _hubContext.Clients.Group(instanceId.ToString()).SendAsync("AnnounceVideo", updatedInstanceQueues?.VideoQueue);
    }

    /// <summary>
    /// Gets the streams of a file that are incompatible with browsers.
    /// <param name="fileMetadata">The files' metadata.</param>
    /// </summary>
    /// <returns>List of indexes of streams that are incompatible with a browser.</returns>
    private List<int> GetIncompatibleStreams(FfprobeMetadata.Metadata fileMetadata) {
        IEnumerable<FfprobeMetadata.Stream> mediaStreams = fileMetadata.Streams.Where(stream => stream.CodecType == FfprobeMetadata.CodecType.Video || stream.CodecType == FfprobeMetadata.CodecType.Audio);
        List<int> streamIds = new List<int>();
        foreach (FfprobeMetadata.Stream mediaStream in mediaStreams) {
            var supportedSteram = FileHelper.CheckSupportedFile(fileMetadata.Format.Container,
                mediaStream.CodecType == FfprobeMetadata.CodecType.Video ? mediaStream.VideoCodecName : null,
                mediaStream.CodecType == FfprobeMetadata.CodecType.Audio ? mediaStream.AudioCodecName : null);

            if (!supportedSteram) {
                streamIds.Add(mediaStream.Index);
            }
        }

        return streamIds;
    }
}