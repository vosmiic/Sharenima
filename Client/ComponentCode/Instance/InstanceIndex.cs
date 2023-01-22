using System.Net;
using System.Net.Http.Json;
using MatBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Sharenima.Client.Helpers;
using Sharenima.Shared;

namespace Sharenima.Client.ComponentCode;

public partial class Instance : ComponentBase {
    [CascadingParameter] private Task<AuthenticationState> authenticationStateTask { get; set; }
    protected bool isLoaded { get; set; }
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; }
    [Inject] private NavigationManager _navigationManager { get; set; }
    [Inject] IAccessTokenProvider TokenProvider { get; set; }
    [Inject] protected IMatToaster _toaster { get; set; }
    [Inject] protected QueuePlayerService QueuePlayerService { get; set; }
    [Inject] protected RefreshService RefreshService { get; set; }
    [Inject] private PermissionService PermissionService { get; set; }
    [Inject] private IJSRuntime _jsRuntime { get; set; }
    [Parameter] public string InstanceId { get; set; }
    protected HubConnection? _hubConnection;
    protected Sharenima.Shared.Instance? SelectedInstance { get; set; }
    protected bool settingsIsOpen = false;
    private HttpClient? _authHttpClient { get; set; }
    private HttpClient _anonymousHttpClient { get; set; }


    protected override async Task OnInitializedAsync() {
        RefreshService.InstanceIndexRefreshRequested += StateChanged;
        var authState = await authenticationStateTask;
        
        if (authState.User.Identity is { IsAuthenticated: true }) {
            _authHttpClient = HttpClientFactory.CreateClient("auth");
        }


        _anonymousHttpClient = HttpClientFactory.CreateClient("anonymous");
        HttpResponseMessage httpResponseMessage = await HttpClient().GetAsync($"Instance?instanceName={InstanceId}&includePermissions=true");

        if (!httpResponseMessage.IsSuccessStatusCode) {
            if (httpResponseMessage.StatusCode == HttpStatusCode.NotFound) {
            }
        }

        Sharenima.Shared.InstanceWithUserPermissions? instance = await httpResponseMessage.Content.ReadFromJsonAsync<Sharenima.Shared.InstanceWithUserPermissions>();
        if (instance == null) return;
        foreach (Permissions.Permission userPermission in instance.UserPermissions) {
            PermissionService.AddToUserPermissions(userPermission);
        }

        SelectedInstance = instance;
        
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_navigationManager.ToAbsoluteUri("/queuehub?instanceId=" + instance.Id), options => {
                options.AccessTokenProvider = async () => {
                    var result = await TokenProvider.RequestAccessToken();
                    if (result.TryGetToken(out var token)) {
                        return token.Value;
                    } else {
                        return string.Empty;
                    }
                };
            })
            .Build();

        await _hubConnection.StartAsync();

        _hubConnection.On<string?, string?, MatToastType>("ToasterError", (title, message, toastType) => { _toaster.Add(message, toastType, title); });

        _hubConnection.On<Permissions.Permission, bool>("PermissionUpdate", (permission, allowed) => {
            if (allowed) {
                PermissionService.AddToUserPermissions(permission);
            } else {
                PermissionService.RemoveFromUserPermissions(permission);
            }
            PermissionService.CallPermissionsUpdated();
        });
        
        isLoaded = true;
    }

    protected void SettingsButtonClicked() =>
        settingsIsOpen = true;

    private Task StateChanged(CancellationToken cancellationToken) {
        StateHasChanged();
        return Task.CompletedTask;
    }

    private HttpClient HttpClient() {
        if (_authHttpClient == null)
            return _anonymousHttpClient;
        return _authHttpClient;
    }
}