using System.Net.Http.Json;
using MatBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace Sharenima.Client.ComponentCode.Stream;

public partial class GoLive : ComponentBase {
    [CascadingParameter] private Task<AuthenticationState> authenticationStateTask { get; set; }
    [Inject] private IJSRuntime _jsRuntime { get; set; }
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; }
    [Inject] protected IMatToaster _toaster { get; set; }
    private HttpClient? _authHttpClient { get; set; }
    private HttpClient _anonymousHttpClient { get; set; }
    private DotNetObjectReference<GoLive>? objRef;
    protected bool streamMediaSelectorDialogIsOpen { get; set; }
    private bool Live { get; set; }
    protected string ButtonClass { get; set; }

    protected override async Task OnInitializedAsync() {
        var authState = await authenticationStateTask;

        if (authState.User.Identity is { IsAuthenticated: true }) {
            _authHttpClient = HttpClientFactory.CreateClient("auth");
        }


        _anonymousHttpClient = HttpClientFactory.CreateClient("anonymous");
        objRef = DotNetObjectReference.Create(this);
        await _jsRuntime.InvokeVoidAsync("setRecordLiveStreamDotNetHelper", objRef);
    }

    protected async void GoLiveClicked() {
        if (Live) {
            await _jsRuntime.InvokeVoidAsync("stopStreaming");
            SwapButtonTheme();
        } else {
            streamMediaSelectorDialogIsOpen = true;
        }
    }

    protected async void OnMediaSelected(bool webcamSlashMic) {
        HttpResponseMessage httpResponseMessage = await _authHttpClient.GetAsync($"/Stream?browserStreaming=true");

        if (!httpResponseMessage.IsSuccessStatusCode) {
            _toaster.Add("Internal server error; check logs", MatToastType.Danger, "Server Error");
        }

        Sharenima.Shared.Stream? stream = await httpResponseMessage.Content.ReadFromJsonAsync<Sharenima.Shared.Stream>();
        if (stream == null) return;
        await _jsRuntime.InvokeVoidAsync("goLive", stream.StreamServer, webcamSlashMic);
    }

    [JSInvokable]
    public void SwapButtonTheme() {
        if (Live) {
            ButtonClass = string.Empty;
            Live = false;
        } else {
            streamMediaSelectorDialogIsOpen = false;
            ButtonClass = "live";
            Live = true;
        }
        StateHasChanged();
    }

    [JSInvokable]
    public async void DisplayStreamingError() {
        _toaster.Add("Streaming error", MatToastType.Danger, "Error");
        await _jsRuntime.InvokeVoidAsync("stopStreaming");
        SwapButtonTheme();
    }
}