using System.Net.Http.Json;
using MatBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace Sharenima.Client.ComponentCode; 

public partial class Queue : ComponentBase {
    [Inject]
    private HttpClient _httpClient { get; set; }
    [Inject]
    protected IMatToaster _toaster { get; set; }
    [Inject]
    private NavigationManager _navigationManager { get; set; }
    [Parameter]
    public Guid InstanceId { get; set; }
    
    public string VideoUrl { get; set; }
    public ICollection<Sharenima.Shared.Queue>? CurrentQueue { get; set; }
    
    

    private HubConnection? _hubConnection;

    protected override async Task OnInitializedAsync() {
        await GetQueue();
        
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_navigationManager.ToAbsoluteUri("/queuehub"))
            .Build();
        
        await _hubConnection.StartAsync();
        await Hub();
    }

    protected async void AddVideoToQueue() {
        var addVideoResponse = await _httpClient.PostAsync($"queue?instanceId={InstanceId}&videoUrl={VideoUrl}", null);

        if (!addVideoResponse.IsSuccessStatusCode) {
            _toaster.Add($"Could not add video; {addVideoResponse.ReasonPhrase}", MatToastType.Danger, "Error");
        }
    }

    protected async void RemoveFromVideoQueue(Guid queueId) {
        var removeVideoResponse = await _httpClient.DeleteAsync($"queue?instanceId={InstanceId}&queueId={queueId}");

        if (!removeVideoResponse.IsSuccessStatusCode) {
            _toaster.Add($"Could not remove video; {removeVideoResponse.ReasonPhrase}", MatToastType.Danger, "Error");
        }
    }

    private async Task GetQueue() {
        var httpResponse = await _httpClient.GetAsync($"queue?instanceId={InstanceId}");

        if (!httpResponse.IsSuccessStatusCode) {
            _toaster.Add("Could not load video queue", MatToastType.Danger, "Error");
        }
        
        ICollection<Sharenima.Shared.Queue>? queueCollection = await httpResponse.Content.ReadFromJsonAsync<ICollection<Sharenima.Shared.Queue>>();
        if (queueCollection != null) {
            CurrentQueue = queueCollection;
        }
    }

    private async Task Hub() {
        await _hubConnection.SendAsync("JoinGroup", InstanceId.ToString());

        _hubConnection.On<Sharenima.Shared.Queue>("AnnounceVideo", (queue) => {
            if (CurrentQueue != null) {
                CurrentQueue.Add(queue);
            } else {
                CurrentQueue = new List<Sharenima.Shared.Queue> { queue };
            }
            StateHasChanged();
        });

        _hubConnection.On<Guid>("RemoveVideo", (queueId) => {
            Sharenima.Shared.Queue? queue = CurrentQueue?.FirstOrDefault(queue => queue.Id == queueId);
            if (queue != null) {
                CurrentQueue.Remove(queue);
                StateHasChanged();
            }
        });
    }
}