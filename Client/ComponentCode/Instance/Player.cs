using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MatBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Sharenima.Shared;
using Sharenima.Shared.Configuration;

namespace Sharenima.Client.ComponentCode;

public partial class Player : ComponentBase {
    [Inject] private IJSRuntime _jsRuntime { get; set; }
    [Inject] private QueuePlayerService QueuePlayerService { get; set; }
    [Inject] protected RefreshService RefreshService { get; set; }
    [Inject] private PermissionService PermissionService { get; set; }
    [Inject] private StreamService StreamService { get; set; }
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; }
    [Inject] protected IMatToaster _toaster { get; set; }
    [Parameter] public HubConnection? HubConnection { get; set; }
    [Parameter] public Guid InstanceId { get; set; }
    [Parameter] public State? InitialState { get; set; }
    [Parameter] public TimeSpan VideoTime { get; set; }
    protected Sharenima.Shared.Queue? Video { get; set; }
    protected string? StreamUrl { get; set; }
    private State playerState { get; set; }
    private DotNetObjectReference<Player>? objRef;
    private bool _initialVideoLoad = true;
    private HttpClient _anonymousHttpClient { get; set; }

    [JSInvokable]
    public void StateChange(int value) {
        if (value is > -1 and < 3 && PermissionService.CheckIfUserHasPermission(Permissions.Permission.ChangeProgress)) {
            playerState = (State)value;
            Console.WriteLine($"State changed: {playerState}");
            HubConnection.SendAsync("SendStateChange", InstanceId, playerState, Video.Id);
        }
    }

    [JSInvokable]
    public async void ProgressChange(Guid videoId, double newTime, bool seeked) {
        if (playerState != State.Ended && videoId == Video?.Id && PermissionService.CheckIfUserHasPermission(Permissions.Permission.ChangeProgress)) {
            if (_initialVideoLoad) {
                if (await SendProgressChange(VideoTime.TotalSeconds, Video?.Id))
                    _initialVideoLoad = false;
            }

            TimeSpan newVideoTime = TimeSpan.FromSeconds(newTime);
            var difference = newVideoTime.TotalMilliseconds - VideoTime.TotalMilliseconds;
            if (difference is > 300 or < -300) {
                HubConnection.SendAsync("SendProgressChange", InstanceId, newVideoTime, seeked, Video?.Id);
                VideoTime = newVideoTime;
            }
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
    public double RequestStoredVideoTime() =>
        VideoTime.TotalSeconds;

    protected override async Task OnInitializedAsync() {
        RefreshService.PlayerRefreshRequested += NewVideo;
        RefreshService.PlayerVideoEnded += EndVideo;
        StreamService.WatchStream += StartWatchingStream;
        objRef = DotNetObjectReference.Create(this);
        await _jsRuntime.InvokeVoidAsync("setDotNetHelper", objRef);
        _anonymousHttpClient = HttpClientFactory.CreateClient("anonymous");

        await Hub();
    }

    private async Task NewVideo(CancellationToken cancellationToken) {
        VideoType? oldVideoType = Video?.VideoType;
        Video = QueuePlayerService.CurrentQueue.FirstOrDefault();
        InitialState = playerState is State.Ended or State.Playing ? State.Playing : State.Paused;
        if (oldVideoType != null && oldVideoType == Video?.VideoType) {
            if (Video?.VideoType == VideoType.YouTube)
                await _jsRuntime.InvokeVoidAsync("changeYTVideoSource", RequestVideo());
            if (Video?.VideoType == VideoType.FileUpload)
                await _jsRuntime.InvokeVoidAsync("changeUploadedVideoSource", Video.Url, playerState is State.Ended or State.Playing);
        } else {
            StateHasChanged();
        }

        StateHasChanged();
    }

    private async Task EndVideo(CancellationToken cancellationToken) {
        if (Video?.VideoType == VideoType.YouTube &&
            QueuePlayerService.CurrentQueue.FirstOrDefault(queue => queue.Order == Video?.Order + 1)?.VideoType != VideoType.YouTube) {
            await _jsRuntime.InvokeVoidAsync("youtubeDestroy");
        } else if (Video?.VideoType == VideoType.FileUpload &&
                   QueuePlayerService.CurrentQueue.FirstOrDefault(queue => queue.Order == Video?.Order + 1)?.VideoType != VideoType.FileUpload) {
            await _jsRuntime.InvokeVoidAsync("destroyVideoElement");
        }

        VideoTime = TimeSpan.Zero;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (Video != null) {
            switch (Video?.VideoType) {
                case VideoType.YouTube:
                    await _jsRuntime.InvokeVoidAsync("runYoutubeApi", RequestVideo(), InitialState is State.Playing, Video.Id);
                    break;
                case VideoType.FileUpload:
                    await _jsRuntime.InvokeVoidAsync("loadVideoFunctions", InitialState is State.Playing, Video.Id);
                    break;
            }
        } else if (StreamUrl != null) {
            await _jsRuntime.InvokeVoidAsync("initializeStreamPlayer", StreamUrl);
        }
    }

    protected override void OnParametersSet() {
        Video = QueuePlayerService.CurrentQueue.FirstOrDefault();
    }

    private async Task Hub() {
        HubConnection.On<State>("ReceiveStateChange", async (state) => {
            switch (state) {
                case State.Playing:
                    switch (Video?.VideoType) {
                        case VideoType.YouTube:
                            await _jsRuntime.InvokeVoidAsync("playYT");
                            break;
                        case VideoType.FileUpload:
                            await _jsRuntime.InvokeVoidAsync("playFileUpload");
                            break;
                    }

                    break;
                case State.Paused:
                    switch (Video?.VideoType) {
                        case VideoType.YouTube:
                            await _jsRuntime.InvokeVoidAsync("pauseYT");
                            break;
                        case VideoType.FileUpload:
                            await _jsRuntime.InvokeVoidAsync("pauseFileUpload");
                            break;
                    }

                    break;
            }
        });

        HubConnection.On<TimeSpan>("ReceiveProgressChange", async (newTime) => {
            var difference = newTime.TotalMilliseconds - VideoTime.TotalMilliseconds;
            if (difference is > 700 or < -700)
                await SendProgressChange(newTime.TotalSeconds, Video?.Id);
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
        StreamUrl = playUrl?.Replace("{User}", StreamService.streamUsername);
        // todo change above; hardcoded during development
        Video = null;
        await _jsRuntime.InvokeVoidAsync("youtubeDestroy");
        
        StateHasChanged();
    }
}