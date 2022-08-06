using System.Net.Http.Json;
using MatBlazor;
using Microsoft.AspNetCore.Components;

namespace Sharenima.Client.ComponentCode; 

public partial class Queue : ComponentBase {
    [Inject]
    private HttpClient _httpClient { get; set; }
    [Inject]
    protected IMatToaster _toaster { get; set; }
    [Parameter]
    public Guid InstanceId { get; set; }
    
    public string VideoUrl { get; set; }
    public ICollection<Sharenima.Shared.Queue>? CurrentQueue { get; set; }

    protected override async Task OnInitializedAsync() {
        var httpResponse = await _httpClient.GetAsync($"queue?instanceId={InstanceId}");

        if (!httpResponse.IsSuccessStatusCode) {
            _toaster.Add("Could not load video queue", MatToastType.Danger, "Error");
        }
        
        ICollection<Sharenima.Shared.Queue>? queueCollection = await httpResponse.Content.ReadFromJsonAsync<ICollection<Sharenima.Shared.Queue>>();
        if (queueCollection != null) {
            CurrentQueue = queueCollection;
        }
    }

    public async void AddVideoToQueue() {
        var createInstanceResponse = await _httpClient.PostAsync($"queue?instanceId={InstanceId}&videoUrl={VideoUrl}", null);

        if (!createInstanceResponse.IsSuccessStatusCode) {
            _toaster.Add($"Could not add video; {createInstanceResponse.ReasonPhrase}", MatToastType.Danger, "Error");
        }
    }
}