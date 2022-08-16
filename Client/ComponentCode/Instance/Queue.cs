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
    [Inject]
    private QueuePlayerService QueuePlayerService { get; set; }
    [Parameter]
    public Guid InstanceId { get; set; }
    [Parameter]
    public ICollection<Sharenima.Shared.Queue>? CurrentQueue { get; set; }
    [Parameter]
    public EventCallback<ICollection<Sharenima.Shared.Queue>?> CurrentQueueChanged { get; set; }
    [Parameter]
    public Player? PlayerSibling { get; set; }
    
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
            await CurrentQueueChanged.InvokeAsync(queueCollection);
            /*CurrentQueue = queueCollection;
            Console.WriteLine(CurrentQueue?.FirstOrDefault()?.Url);*/
            QueuePlayerService.CallRequestRefresh();
        }
    }

    private async Task Hub() {
        await HubConnection.SendAsync("JoinGroup", InstanceId.ToString());

        HubConnection.On<Sharenima.Shared.Queue>("AnnounceVideo", async (queue) => {
            if (CurrentQueue != null) {
                CurrentQueue.Add(queue);
            } else {
                CurrentQueue = new List<Sharenima.Shared.Queue> { queue };
            }
            await CurrentQueueChanged.InvokeAsync(CurrentQueue);
            StateHasChanged();
        });

        HubConnection.On<Guid>("RemoveVideo", async (queueId) => {
            Sharenima.Shared.Queue? queue = CurrentQueue?.FirstOrDefault(queue => queue.Id == queueId);
            if (queue != null) {
                CurrentQueue.Remove(queue);
                await CurrentQueueChanged.InvokeAsync(CurrentQueue);
                StateHasChanged();
            }
        });

        HubConnection.On<int>("ReceiveStateChange", async (state) => {
            if (state == 0) {
                CurrentQueue.Remove(CurrentQueue.First());
                await CurrentQueueChanged.InvokeAsync(CurrentQueue);
                StateHasChanged();
                QueuePlayerService.CallChangeVideo();
            }
        });
    }
}