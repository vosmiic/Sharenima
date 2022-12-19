using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Sharenima.Shared;

namespace Sharenima.Client.ComponentCode;

public partial class Player : ComponentBase {
    [Inject] private IJSRuntime _jsRuntime { get; set; }
    [Inject] private QueuePlayerService QueuePlayerService { get; set; }
    [Inject] protected RefreshService RefreshService { get; set; }
    [Parameter] public HubConnection? HubConnection { get; set; }
    [Parameter] public Guid InstanceId { get; set; }
    [Parameter] public State? InitialState { get; set; }
    [Parameter] public TimeSpan VideoTime { get; set; }
    protected Sharenima.Shared.Queue? Video { get; set; }
    private State playerState { get; set; }
    private DotNetObjectReference<Player>? objRef;
    private bool _initialVideoLoad = true;

    [JSInvokable]
    public void StateChange(int value) {
        if (value is > -1 and < 3) {
            playerState = (State)value;
            Console.WriteLine($"State changed: {playerState}");
            HubConnection.SendAsync("SendStateChange", InstanceId, playerState, Video.Id);
        }
    }

    [JSInvokable]
    public async void ProgressChange(double newTime, bool seeked) {
        if (playerState != State.Ended) {
            if (_initialVideoLoad) {
                if (await SendProgressChange(VideoTime.TotalSeconds, Video?.Id))
                    _initialVideoLoad = false;
            }

            TimeSpan newVideoTime = TimeSpan.FromSeconds(newTime);
            var difference = newVideoTime.TotalMilliseconds - VideoTime.TotalMilliseconds;
            if (difference is > 300 or < -300) {
                Console.WriteLine("Sending progress change");
                HubConnection.SendAsync("SendProgressChange", InstanceId, newVideoTime, seeked);
                VideoTime = newVideoTime;
            }
        } else {
            Console.WriteLine("Video has ended; not updating db");
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
        objRef = DotNetObjectReference.Create(this);
        await _jsRuntime.InvokeVoidAsync("setDotNetHelper", objRef);

        await Hub();
    }

    private void NewVideo() {
        Video = QueuePlayerService.CurrentQueue.FirstOrDefault();
        InitialState = State.Playing;
        StateHasChanged();
    }

    private async void EndVideo() {
        if (Video?.VideoType == VideoType.YouTube) {
            await _jsRuntime.InvokeVoidAsync("youtubeDestroy");
        }
        VideoTime = TimeSpan.Zero;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (Video != null) {
            switch (Video?.VideoType) {
                case VideoType.YouTube:
                    await _jsRuntime.InvokeVoidAsync("runYoutubeApi", RequestVideo(), InitialState is State.Playing);
                    break;
                case VideoType.FileUpload:
                    await _jsRuntime.InvokeVoidAsync("loadVideoFunctions", InitialState is State.Playing, Video.Id);
                    break;
            }
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
}