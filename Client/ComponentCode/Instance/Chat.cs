using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using Sharenima.Shared;

namespace Sharenima.Client.ComponentCode;

public partial class Chat : ComponentBase {
    [CascadingParameter] private Task<AuthenticationState> authenticationStateTask { get; set; }
    private HttpClient? _authHttpClient { get; set; }
    private HttpClient _anonymousHttpClient { get; set; }
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; }
    [Inject] private HubService HubService { get; set; }
    [Parameter] public Guid InstanceId { get; set; }
    [Parameter] public HubConnection? HubConnection { get; set; }
    protected List<string> UserList { get; set; } = new();
    protected int OnlineUsers { get; set; }

    protected override async Task OnInitializedAsync() {
        var authState = await authenticationStateTask;
        if (authState.User.Identity is { IsAuthenticated: true })
            _authHttpClient = HttpClientFactory.CreateClient("auth");
        _anonymousHttpClient = HttpClientFactory.CreateClient("anonymous");

        HubService.userJoined += ( username,  _) => {
            if (!UserList.Contains(username)) UserList.Add(username);

            RefreshOnlineUserCount();
            StateHasChanged();
            return Task.CompletedTask;
        };

        HubConnection.On<string>("UserLeft", (username) => {
            UserList.Remove(username);

            RefreshOnlineUserCount();
            StateHasChanged();
        });
    }

    protected override async Task OnParametersSetAsync() {
        var httpResponse = await _anonymousHttpClient.GetAsync($"instance/users?instanceId={InstanceId}");
        if (httpResponse.IsSuccessStatusCode && httpResponse.StatusCode != HttpStatusCode.NoContent) {
            UserList = await httpResponse.Content.ReadFromJsonAsync<List<string>>();
            RefreshOnlineUserCount();
        }
    }

    private void RefreshOnlineUserCount() =>
        OnlineUsers = UserList.Count;
}