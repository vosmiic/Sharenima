using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace Sharenima.Client.ComponentCode.Stream;

public partial class Stream : ComponentBase, IAsyncDisposable {
    [Inject] private IJSRuntime _jsRuntime { get; set; }
    [Parameter] public string PlayUrl { get; set; }
    private IJSObjectReference? module;

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender) {
            module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./Pages/Stream/Stream.razor.js");

            await module.InvokeVoidAsync("initializeStreamPlayer", PlayUrl);
        } else {
            await module.InvokeVoidAsync("loadNewStream", PlayUrl);
        }
    }

    public async ValueTask DisposeAsync() {
        if (module is not null) {
            await module.DisposeAsync();
        }
    }
}