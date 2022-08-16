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

    [JSInvokable]
    public void StateChange(int value) {
        if (value is > -1 and < 3) {
            HubConnection.SendAsync("SendStateChange", InstanceId, value, Video.Id);
        }
    }

    [JSInvokable]
    public void ProgressChange(double newTime) {
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
            if ((newTime - VideoTime) > TimeSpan.FromMilliseconds(500))
                await _jsRuntime.InvokeVoidAsync("setCurrentTime", newTime.TotalSeconds);
        });
    }

    private void RefreshState() {
        StateHasChanged();
    }

    private async void ChangeVideo() {
        await _jsRuntime.InvokeVoidAsync("loadVideo", RequestVideo());
    }
}