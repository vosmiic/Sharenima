using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Sharenima.Shared;

namespace Sharenima.Client.ComponentCode;

public partial class Player : ComponentBase {
    [Inject] private IJSRuntime _jsRuntime { get; set; }
    [Inject] private QueuePlayerService QueuePlayerService { get; set; }
    [Inject]
    protected RefreshService RefreshService { get; set; }
    [Parameter] public HubConnection? HubConnection { get; set; }
    [Parameter] public Guid InstanceId { get; set; }
    [Parameter] public TimeSpan VideoTime { get; set; }
    protected Sharenima.Shared.Queue? Video { get; set; }
    private int playerState { get; set; }
    private DotNetObjectReference<Player>? objRef;
    private bool _initialVideoLoad = true;

    [JSInvokable]
    public void StateChange(int value) {
        if (value is > -1 and < 3) {
            Console.WriteLine($"State changed: {value}");
            playerState = value;
            HubConnection.SendAsync("SendStateChange", InstanceId, value, Video.Id);
        }
    }

    [JSInvokable]
    public async void ProgressChange(double newTime) {
        if (playerState != 0) {
            if (_initialVideoLoad) {
                if (await SendProgressChange(VideoTime.TotalSeconds))
                    _initialVideoLoad = false;
            }

            TimeSpan newVideoTime = TimeSpan.FromSeconds(newTime);
            var difference = newVideoTime.TotalMilliseconds - VideoTime.TotalMilliseconds;
            if (difference is > 300 or < -300) {
                Console.WriteLine("Sending progress change");
                HubConnection.SendAsync("SendProgressChange", InstanceId, newVideoTime);
                VideoTime = newVideoTime;
            }
        } else {
            Console.WriteLine("Video has ended; not updating db");
        }
    }

    [JSInvokable]
    public string RequestVideo() {
        int lastIndex = Video.Url.LastIndexOf("?v=", StringComparison.CurrentCulture) + 3;
        return Video.Url.Substring(lastIndex, Video.Url.Length - lastIndex);
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
        objRef = DotNetObjectReference.Create(this);
        await _jsRuntime.InvokeVoidAsync("setDotNetHelper", objRef);

        await Hub();
    }

    private void NewVideo() {
        Video = QueuePlayerService.CurrentQueue.FirstOrDefault();
        StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (Video != null) {
            switch (Video?.VideoType) {
                case VideoType.YouTube:
                    await _jsRuntime.InvokeVoidAsync("runYoutubeApi", RequestVideo());
                    break;
                case VideoType.FileUpload:
                    await _jsRuntime.InvokeVoidAsync("loadVideoFunctions");
                    break;
            }
        }
    }

    protected override void OnParametersSet() {
        Video = QueuePlayerService.CurrentQueue.FirstOrDefault();
    }

    private async Task Hub() {
        HubConnection.On<int>("ReceiveStateChange", async (state) => {
            switch (state) {
                case 0:
                    //unload player
                    await _jsRuntime.InvokeVoidAsync("youtubeDestroy");
                    VideoTime = TimeSpan.Zero;
                    break;
                case 1:
                    switch (Video?.VideoType) {
                        case VideoType.YouTube:
                            await _jsRuntime.InvokeVoidAsync("playYT");
                            break;
                        case VideoType.FileUpload:
                            await _jsRuntime.InvokeVoidAsync("playFileUpload");
                            break;
                    }
                    break;
                case 2:
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
                await SendProgressChange(newTime.TotalSeconds);
        });
    }

    private async Task<bool> SendProgressChange(double totalSeconds) {
        switch (Video?.VideoType) {
            case VideoType.YouTube:
                return await _jsRuntime.InvokeAsync<bool>("setCurrentYoutubeVideoTime", totalSeconds);
            case VideoType.FileUpload:
                return await _jsRuntime.InvokeAsync<bool>("setCurrentUploadedVideoTime", totalSeconds);
        }

        return false;
    }
}