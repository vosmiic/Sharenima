using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace Sharenima.Client.ComponentCode;

public partial class Player : ComponentBase {
    [Inject] private IJSRuntime _jsRuntime { get; set; }
    [Inject] private RefreshService RefreshService { get; set; }
    [Parameter] public HubConnection? HubConnection { get; set; }
    [Parameter] public Guid InstanceId { get; set; }
    [Parameter] public TimeSpan VideoTime { get; set; }
    [Parameter] public string? VideoId { get; set; }
    private DotNetObjectReference<Player>? objRef;

    [JSInvokable]
    public void StateChange(int value) {
        if (value is > -1 and < 3) {
            HubConnection.SendAsync("SendStateChange", InstanceId, value);
        }
    }

    [JSInvokable]
    public void ProgressChange(double newTime) {
        TimeSpan newVideoTime = TimeSpan.FromSeconds(newTime);
        var difference = newVideoTime - VideoTime;
        if (difference > TimeSpan.FromMilliseconds(500)) {
            HubConnection.SendAsync("SendProgressChange", InstanceId, newVideoTime);
            VideoTime = newVideoTime;
        }
    }

    [JSInvokable]
    public string RequestVideo() {
        int lastIndex = VideoId.LastIndexOf("?v=", StringComparison.CurrentCulture) + 3;
        return VideoId.Substring(lastIndex, VideoId.Length - lastIndex);
    }

    protected override async Task OnInitializedAsync() {
        RefreshService.RefreshRequested += RefreshState;
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
}