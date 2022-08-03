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

    /*protected override Task OnInitializedAsync() {
        return null;
    }*/

    public async void AddVideoToQueue() {
        var createInstanceResponse = await _httpClient.PostAsync($"queue?instanceId={InstanceId}&videoUrl={VideoUrl}", null);

        if (!createInstanceResponse.IsSuccessStatusCode) {
            _toaster.Add($"Could not add video; {createInstanceResponse.ReasonPhrase}", MatToastType.Danger, "Error");
        }
    }
}