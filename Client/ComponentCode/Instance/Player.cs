using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace Sharenima.Client.ComponentCode;

public partial class Player : ComponentBase {
    [Inject] private IJSRuntime _jsRuntime { get; set; }
    [Inject] private QueuePlayerService QueuePlayerService { get; set; }
    [Parameter] public HubConnection? HubConnection { get; set; }
    [Parameter] public Guid InstanceId { get; set; }
    [Parameter] public TimeSpan VideoTime { get; set; }
    [Parameter] public Sharenima.Shared.Queue? Video { get; set; }
    private DotNetObjectReference<Player>? objRef;
    private bool _initialVideoLoad = true;

    [JSInvokable]
    public void StateChange(int value) {
        if (value is > -1 and < 3) {
            HubConnection.SendAsync("SendStateChange", InstanceId, value, Video.Id);
        }
    }

    [JSInvokable]
    public async void ProgressChange(double newTime) {
        if (_initialVideoLoad) {
            if (await SendProgressChange(VideoTime.TotalSeconds))
                _initialVideoLoad = false;
        }
        TimeSpan newVideoTime = TimeSpan.FromSeconds(newTime);
        var difference = newVideoTime.TotalMilliseconds - VideoTime.TotalMilliseconds;
        if (difference is > 500 or < -500) {
            HubConnection.SendAsync("SendProgressChange", InstanceId, newVideoTime);
            VideoTime = newVideoTime;
        }
    }

    [JSInvokable]
    public string RequestVideo() {
        int lastIndex = Video.Url.LastIndexOf("?v=", StringComparison.CurrentCulture) + 3;
        return Video.Url.Substring(lastIndex, Video.Url.Length - lastIndex);
    }

    protected override async Task OnInitializedAsync() {
        QueuePlayerService.RefreshRequested += RefreshState;
        QueuePlayerService.ChangeVideo += ChangeVideo;
        objRef = DotNetObjectReference.Create(this);
        await _jsRuntime.InvokeVoidAsync("setDotNetHelper", objRef);
        await _jsRuntime.InvokeVoidAsync("runYoutubeApi");
        await Hub();
    }

    protected async Task Play() {
        await _jsRuntime.InvokeVoidAsync("playYT");
    }

    private async Task Hub() {
        HubConnection.On<int>("ReceiveStateChange", async (state) => {
            switch (state) {
                case 0:
                    //unload player
                    break;
                case 1:
                    await _jsRuntime.InvokeVoidAsync("playYT");
                    break;
                case 2:
                    await _jsRuntime.InvokeVoidAsync("pauseYT");
                    break;
            }
        });

        HubConnection.On<TimeSpan>("ReceiveProgressChange", async (newTime) => {
            var difference = newTime.TotalMilliseconds - VideoTime.TotalMilliseconds;
            if (difference is > 600 or < -600)
                await SendProgressChange(newTime.TotalSeconds);
        });
    }

    private async Task<bool> SendProgressChange(double totalSeconds) {
        return await _jsRuntime.InvokeAsync<bool>("setCurrentTime", totalSeconds);
    }

    private void RefreshState() {
        StateHasChanged();
    }

    private async void ChangeVideo() {
        await _jsRuntime.InvokeVoidAsync("loadVideo", RequestVideo());
    }
}