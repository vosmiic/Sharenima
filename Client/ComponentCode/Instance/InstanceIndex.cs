using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Sharenima.Client.ComponentCode;

public partial class Instance : ComponentBase {
    protected bool isLoaded { get; set; }
    [Inject] private HttpClient _httpClient { get; set; }
    [Inject] private NavigationManager _navigationManager { get; set; }

    [Parameter] public string InstanceId { get; set; }
    protected HubConnection? _hubConnection;
    protected Sharenima.Shared.Instance? SelectedInstance { get; set; }

    protected ICollection<Sharenima.Shared.Queue>? CurrentQueue { get; set; }


    protected override async Task OnInitializedAsync() {
        HttpResponseMessage httpResponseMessage = await _httpClient.GetAsync($"Instance?instanceName={InstanceId}");

        if (!httpResponseMessage.IsSuccessStatusCode) {
            if (httpResponseMessage.StatusCode == HttpStatusCode.NotFound) {
            }
        }

        Sharenima.Shared.Instance? instance = await httpResponseMessage.Content.ReadFromJsonAsync<Sharenima.Shared.Instance>();
        if (instance == null) return;

        SelectedInstance = instance;
        
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_navigationManager.ToAbsoluteUri("/queuehub"))
            .Build();
        
        await _hubConnection.StartAsync();
        
        isLoaded = true;
    }
}