using System.Net;
using System.Net.Http.Json;
using MatBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.SignalR.Client;

namespace Sharenima.Client.ComponentCode;

public partial class Instance : ComponentBase {
    protected bool isLoaded { get; set; }
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; }
    [Inject] private NavigationManager _navigationManager { get; set; }
    [Inject] IAccessTokenProvider TokenProvider { get; set; }
    [Inject] protected IMatToaster _toaster { get; set; }
    [Inject]
    protected QueuePlayerService QueuePlayerService { get; set; }
    [Inject]
    protected RefreshService RefreshService { get; set; }
    [Parameter] public string InstanceId { get; set; }
    protected HubConnection? _hubConnection;
    protected Sharenima.Shared.Instance? SelectedInstance { get; set; }
    protected bool settingsIsOpen = false;
    private HttpClient _httpClient { get; set; }


    protected override async Task OnInitializedAsync() {
        RefreshService.InstanceIndexRefreshRequested += StateHasChanged;

        _httpClient = HttpClientFactory.CreateClient("anonymous");
        HttpResponseMessage httpResponseMessage = await _httpClient.GetAsync($"Instance?instanceName={InstanceId}");

        if (!httpResponseMessage.IsSuccessStatusCode) {
            if (httpResponseMessage.StatusCode == HttpStatusCode.NotFound) {
            }
        }

        Sharenima.Shared.Instance? instance = await httpResponseMessage.Content.ReadFromJsonAsync<Sharenima.Shared.Instance>();
        if (instance == null) return;

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

        _hubConnection.On<string?, string?, MatToastType>("ToasterError", (title, message, toastType) => {
            _toaster.Add(message, toastType, title);
        });

        isLoaded = true;
    }

    protected void SettingsButtonClicked() =>
        settingsIsOpen = true;
}