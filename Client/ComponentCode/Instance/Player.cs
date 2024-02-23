using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MatBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Sharenima.Client.Models;
using Sharenima.Shared;
using Sharenima.Shared.Configuration;
using Sharenima.Shared.Queue;

namespace Sharenima.Client.ComponentCode;

public partial class Player : ComponentBase {
    [CascadingParameter] private Task<AuthenticationState> authenticationStateTask { get; set; }
    [Inject] private IJSRuntime _jsRuntime { get; set; }
    [Inject] private QueuePlayerService QueuePlayerService { get; set; }
    [Inject] protected RefreshService RefreshService { get; set; }
    [Inject] private PermissionService PermissionService { get; set; }
    [Inject] private StreamService StreamService { get; set; }
    [Inject] private HubService HubService { get; set; }
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; }
    [Inject] protected IMatToaster _toaster { get; set; }
    [Parameter] public HubConnection? HubConnection { get; set; }
    [Parameter] public Guid InstanceId { get; set; }
    [Parameter] public State? InitialState { get; set; }
    [Parameter] public TimeSpan VideoTime { get; set; }
    protected Sharenima.Shared.Queue.Queue? Video { get; set; }
    protected string? StreamUrl { get; set; }
    private State playerState { get; set; }
    private DotNetObjectReference<Player>? objRef;
    private bool _initialVideoLoad = true;
    private bool _videoReady;
    private HttpClient? _authHttpClient { get; set; }
    private HttpClient _anonymousHttpClient { get; set; }

    [JSInvokable]
    public async Task StateChange(int value, int previousValue, Guid videoId) {
        if (videoId == Video?.Id && value is > -1 and < 3 && PermissionService.CheckIfUserHasPermission(Permissions.Permission.ChangeProgress)) {
            playerState = (State)value;
            Console.WriteLine($"State changed: {playerState}");
            var updateResponse = await (_authHttpClient ?? _anonymousHttpClient).PostAsync($"queue/statechange?instanceId={InstanceId}&playerState={playerState}&queueId={Video.Id}", null);
            if (!updateResponse.IsSuccessStatusCode) {
                await _jsRuntime.InvokeVoidAsync("ytCommandFromHub", previousValue);
            }
            if (value is 0)
                Console.WriteLine($"sent video {Video.Id} has ended alert");
        }
    }

    [JSInvokable]
    public async void ProgressChange(Guid videoId, double newTime, bool seeked) {
        if ((HubService.IsLeader || seeked) && _videoReady && playerState != State.Ended && videoId == Video?.Id && PermissionService.CheckIfUserHasPermission(Permissions.Permission.ChangeProgress)) {
            if (_initialVideoLoad) {
                if (await SendProgressChange(newTime, Video?.Id))
                    _initialVideoLoad = false;
                return;
            }

            TimeSpan newVideoTime = TimeSpan.FromSeconds(newTime);
            HubConnection.SendAsync("SendProgressChange", InstanceId, newVideoTime, seeked, Video?.Id);
        }
    }

    [JSInvokable]
    public string RequestVideo() {
        Regex regex = new Regex(@"(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\s]{11})", RegexOptions.IgnoreCase);

        MatchCollection matchCollection = regex.Matches(Video.Url);
        string videoId = "";
        foreach (Match match in matchCollection) {
            GroupCollection groupCollection = match.Groups;
            videoId = groupCollection[1].Value;
        }

        return videoId;
    }

    /// <summary>
    /// Gets the current time of the video stored in the database. Requested when the video frame has loaded in to keep it up to date.
    /// </summary>
    /// <returns>Current video time.</returns>
    [JSInvokable]
    public double RequestInitialVideoTime() {
        var time = VideoTime.TotalSeconds;
        VideoTime = TimeSpan.MinValue;
        return time;
    }

    [JSInvokable]
    public void SetReady(bool ready) => _videoReady = ready;

    protected override async Task OnInitializedAsync() {
        RefreshService.PlayerRefreshRequested += NewVideo;
        RefreshService.PlayerVideoEnded += EndVideo;
        StreamService.WatchStream += StartWatchingStream;
        objRef = DotNetObjectReference.Create(this);
        await _jsRuntime.InvokeVoidAsync("setDotNetHelper", objRef);
        _anonymousHttpClient = HttpClientFactory.CreateClient("anonymous");
        var authState = await authenticationStateTask;
        if (authState.User.Identity is { IsAuthenticated: true }) {
            _authHttpClient = HttpClientFactory.CreateClient("auth");
        }

        await Hub();
    }

    private async Task NewVideo(CancellationToken cancellationToken) {
        VideoType? oldVideoType = Video?.VideoType;
        Video = QueuePlayerService.CurrentQueue.FirstOrDefault();
        InitialState = playerState is State.Ended or State.Playing ? State.Playing : State.Paused;
        bool playerLoaded = false;
        switch (oldVideoType) {
            case VideoType.YouTube:
                playerLoaded = await _jsRuntime.InvokeAsync<bool>("youtubeCheckIfPlayerExists", cancellationToken);
                break;
            case VideoType.FileUpload:
                playerLoaded = await _jsRuntime.InvokeAsync<bool>("uploadCheckIfPlayerExists", cancellationToken);
                break;
        }
        if (oldVideoType != null && oldVideoType == Video?.VideoType && playerLoaded) {
            if (Video?.VideoType == VideoType.YouTube)
                await _jsRuntime.InvokeVoidAsync("changeYTVideoSource", RequestVideo());
            if (Video?.VideoType == VideoType.FileUpload)
                await _jsRuntime.InvokeVoidAsync("changeUploadedVideoSource", OvenPlayerMediaList.GenerateMediaListFromQueue(Video), playerState is State.Ended or State.Playing);
        } else {
            StateHasChanged();
        }

        StateHasChanged();
    }

    private Task EndVideo(CancellationToken cancellationToken) => EndVideo();

    private async Task EndVideo(bool forceDestruction = false) {
        _videoReady = false;
        if (Video?.VideoType == VideoType.YouTube &&
            (forceDestruction || QueuePlayerService.GetCurrentQueue()?.VideoType != VideoType.YouTube)) {
            await _jsRuntime.InvokeVoidAsync("youtubeDestroy");
        } else if (Video?.VideoType == VideoType.FileUpload &&
                   (forceDestruction || QueuePlayerService.GetCurrentQueue()?.VideoType != VideoType.FileUpload)) {
            await _jsRuntime.InvokeVoidAsync("destroyVideoElement");
        }
    }

    [JSInvokable]
    public void OnPlayerError(string error, Guid videoId) {
        if (Video?.VideoType == VideoType.YouTube) {
            switch (Int32.Parse(error)) {
                case 101:
                case 150:
                    _toaster.Add("Video cannot be played; video is non-embeddable", MatToastType.Danger, "Error");
                    break;
                default:
                    _toaster.Add("Video cannot be played", MatToastType.Danger, "Error");
                    break;
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (Video != null) {
            switch (Video?.VideoType) {
                case VideoType.YouTube:
                    await _jsRuntime.InvokeVoidAsync("runYoutubeApi", RequestVideo(), InitialState is State.Playing, Video.Id);
                    break;
                case VideoType.FileUpload:
                    await _jsRuntime.InvokeVoidAsync("loadVideoFunctions", InitialState is State.Playing, Video.Id, OvenPlayerMediaList.GenerateMediaListFromQueue(Video));
                    break;
            }
        } else if (StreamUrl != null) {
            await _jsRuntime.InvokeVoidAsync("initializeStreamPlayer", StreamUrl);
        }
    }

    protected override void OnParametersSet() {
        Video = QueuePlayerService.CurrentQueue.MinBy(queue => queue.Order);
    }

    private async Task Hub() {
        HubConnection.On<State>("ReceiveStateChange", async (state) => {
            switch (state) {
                case State.Playing:
                    switch (Video?.VideoType) {
                        case VideoType.YouTube:
                            await _jsRuntime.InvokeVoidAsync("ytCommandFromHub", 1);
                            break;
                        case VideoType.FileUpload:
                            await _jsRuntime.InvokeVoidAsync("playFileUpload");
                            break;
                    }

                    break;
                case State.Paused:
                    switch (Video?.VideoType) {
                        case VideoType.YouTube:
                            await _jsRuntime.InvokeVoidAsync("ytCommandFromHub", 2);
                            break;
                        case VideoType.FileUpload:
                            await _jsRuntime.InvokeVoidAsync("pauseFileUpload");
                            break;
                    }

                    break;
            }
        });

        HubConnection.On<TimeSpan, Guid?>("ReceiveProgressChange", async (newTime, videoId) => {
            if (Video?.Id == videoId) {
                _initialVideoLoad = false;
                TimeSpan mediaTime = await GetMediaTime();
                var difference = newTime.TotalMilliseconds - mediaTime.TotalMilliseconds;
                if (difference is > 250 or < -250)
                    await SendProgressChange(newTime.TotalSeconds, Video?.Id);
            }
        });
    }

    private async Task<bool> SendProgressChange(double totalSeconds, Guid? videoId) {
        switch (Video?.VideoType) {
            case VideoType.YouTube:
                return await _jsRuntime.InvokeAsync<bool>("setCurrentYoutubeVideoTime", totalSeconds, videoId);
            case VideoType.FileUpload:
                return await _jsRuntime.InvokeAsync<bool>("setCurrentUploadedVideoTime", totalSeconds, videoId);
        }

        return false;
    }

    public async Task StartWatchingStream(CancellationToken cancellationToken) {
        HttpResponseMessage httpResponseMessage = await _anonymousHttpClient.GetAsync($"config?configKey={ConfigKey.BasePlayUrl}");
        if (!httpResponseMessage.IsSuccessStatusCode) _toaster.Add("Could not connect to server", MatToastType.Danger, "Connection Error");
        string? playUrl = await httpResponseMessage.Content.ReadAsStringAsync();
        bool streamAlreadyPlaying = StreamUrl != null;
        StreamUrl = playUrl?.Replace("{User}", StreamService.streamUsername);
        await HubConnection.SendAsync("RevokeLeadership", InstanceId, cancellationToken: cancellationToken);
        if (streamAlreadyPlaying) {
            await _jsRuntime.InvokeVoidAsync("loadNewStream", StreamUrl);
        } else {
            Video = null;
            _videoReady = false;
            await _jsRuntime.InvokeVoidAsync("youtubeDestroy");

            StateHasChanged();
        }
    }

    public async Task CloseStreamClicked() {
        await HubConnection.SendAsync("EnableLeader", InstanceId, true);
        StreamUrl = null;
        _initialVideoLoad = true;
        await NewVideo(new CancellationToken());
    }

    private async Task<TimeSpan> GetMediaTime() {
        double time = 0;
        switch (Video?.VideoType) {
            case VideoType.YouTube:
                time = await _jsRuntime.InvokeAsync<double>("getCurrentTime");
                break;
            case VideoType.FileUpload:
                time = await _jsRuntime.InvokeAsync<double>("uploadGetTime");
                break;
        }

        return TimeSpan.FromSeconds(time);
    }
}