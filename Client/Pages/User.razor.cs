using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Sharenima.Client.Pages;

public partial class User : ComponentBase {
    [CascadingParameter] private Task<AuthenticationState> authenticationStateTask { get; set; }
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; }
    [Parameter] public string Username { get; set; }
    private HttpClient? _authHttpClient { get; set; }
    private HttpClient _anonymousHttpClient { get; set; }
    protected Sharenima.Shared.Stream? SelectedStream { get; set; }
    protected bool Owner { get; set; }


    protected override async Task OnParametersSetAsync() {
        var authState = await authenticationStateTask;

        if (authState.User.Identity?.Name == Username) {
            Owner = true;
        }

        if (authState.User.Identity is { IsAuthenticated: true }) {
            _authHttpClient = HttpClientFactory.CreateClient("auth");
        }

        _anonymousHttpClient = HttpClientFactory.CreateClient("anonymous");

        HttpResponseMessage httpResponseMessage;
        if (_authHttpClient != null) {
            httpResponseMessage = await _authHttpClient.GetAsync($"stream?username={Username}");
        } else {
            httpResponseMessage = await _anonymousHttpClient.GetAsync($"stream?username={Username}");
        }

        if (!httpResponseMessage.IsSuccessStatusCode) return;
        SelectedStream = await httpResponseMessage.Content.ReadFromJsonAsync<Sharenima.Shared.Stream>();
    }

    protected async Task GenerateStreamUrl() {
        if (_authHttpClient == null) return;
        HttpResponseMessage httpResponseMessage = await _authHttpClient.PostAsync($"stream/create", null);
        Sharenima.Shared.Stream? responseStream = await httpResponseMessage.Content.ReadFromJsonAsync<Sharenima.Shared.Stream>();
        if (responseStream != null) {
            SelectedStream ??= new Sharenima.Shared.Stream();

            SelectedStream.StreamServer = responseStream.StreamServer;
            SelectedStream.StreamKey = responseStream.StreamKey;
        }
    }
}