using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Sharenima.Client.ComponentCode; 

public partial class Index : ComponentBase {
    [CascadingParameter]
    private Task<AuthenticationState> authenticationStateTask { get; set; }
    [Inject]
    private IHttpClientFactory HttpClientFactory { get; set; }
    private HttpClient? _httpClient { get; set; }
    protected string InstanceName { get; set; }

    protected override async Task OnInitializedAsync() {
        var authState = await authenticationStateTask;
        if (authState.User.Identity is { IsAuthenticated: true })
            _httpClient = HttpClientFactory.CreateClient("auth");
    }

    [Authorize]
    protected async Task<bool> CreateInstance() {
        if (_httpClient == null) return false;
        //todo handle above
        var createInstanceResponse = await _httpClient.PostAsync($"instance?instanceName={InstanceName}", null);
        return createInstanceResponse.IsSuccessStatusCode;
    }
}