using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace Sharenima.Client.ComponentCode; 

public partial class Player : ComponentBase {
    [Inject] private IJSRuntime _jsRuntime { get; set; }
    [Parameter] public HubConnection? HubConnection { get; set; }
    [Parameter] public Guid InstanceId { get; set; } 
    private DotNetObjectReference<Player>? objRef;
    
    [JSInvokable]
    public void StateChange(int value) {
        if (value is > -1 and < 3) {
            HubConnection.SendAsync("SendStateChange", InstanceId, value);
        }
    }

    protected override async Task OnInitializedAsync() {
        await _jsRuntime.InvokeVoidAsync("runYoutubeApi");
        objRef = DotNetObjectReference.Create(this);
        await _jsRuntime.InvokeVoidAsync("setDotNetHelper", objRef);
        await _jsRuntime.InvokeVoidAsync("onYouTubeIframeAPIReady");
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
    }
}