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
    [Parameter]
    public ICollection<Sharenima.Shared.Queue>? CurrentQueue { get; set; }
    
    public string VideoUrl { get; set; }

    [Parameter]
    public HubConnection? HubConnection { get; set; }

    protected override async Task OnInitializedAsync() {
        await GetQueue();
        
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
        await HubConnection.SendAsync("JoinGroup", InstanceId.ToString());

        HubConnection.On<Sharenima.Shared.Queue>("AnnounceVideo", (queue) => {
            if (CurrentQueue != null) {
                CurrentQueue.Add(queue);
            } else {
                CurrentQueue = new List<Sharenima.Shared.Queue> { queue };
            }
            StateHasChanged();
        });

        HubConnection.On<Guid>("RemoveVideo", (queueId) => {
            Sharenima.Shared.Queue? queue = CurrentQueue?.FirstOrDefault(queue => queue.Id == queueId);
            if (queue != null) {
                CurrentQueue.Remove(queue);
                StateHasChanged();
            }
        });
    }
}