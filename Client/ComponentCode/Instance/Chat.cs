using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;

namespace Sharenima.Client.ComponentCode; 

public partial class Chat : ComponentBase {
    [CascadingParameter]
    private Task<AuthenticationState> authenticationStateTask { get; set; }
    private HttpClient? _authHttpClient { get; set; }
    private HttpClient _anonymousHttpClient { get; set; }
    [Inject]
    private IHttpClientFactory HttpClientFactory { get; set; }
    [Parameter] public Guid InstanceId { get; set; }
    [Parameter]
    public HubConnection? HubConnection { get; set; }
    protected List<string>? UserList { get; set; }

    protected override async Task OnInitializedAsync() {
        var authState = await authenticationStateTask;
        if (authState.User.Identity is { IsAuthenticated: true })
            _authHttpClient = HttpClientFactory.CreateClient("auth");
        _anonymousHttpClient = HttpClientFactory.CreateClient("anonymous");

        HubConnection.On<Guid>("UserJoined", (userId) => {
            if (UserList != null) {
                if (!UserList.Contains(userId.ToString())) UserList.Add(userId.ToString());
            } else {
                UserList = new List<string>();
                UserList.Add(userId.ToString());
            }
            StateHasChanged();
        });

        HubConnection.On<Guid>("UserLeft", (userId) => {
            if (UserList != null) {
                UserList.Remove(userId.ToString());
            } else {
                UserList = new List<string>();
                UserList.Remove(userId.ToString());
            }
            StateHasChanged();
        });
    }

    protected override async Task OnParametersSetAsync() {
        var httpResponse = await _anonymousHttpClient.GetAsync($"instance/users?instanceId={InstanceId}");
        if (httpResponse.IsSuccessStatusCode && httpResponse.StatusCode != HttpStatusCode.NoContent)
            UserList = await httpResponse.Content.ReadFromJsonAsync<List<string>>();
    }
}