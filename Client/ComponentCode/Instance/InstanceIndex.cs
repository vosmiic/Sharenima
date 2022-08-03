using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;

namespace Sharenima.Client.ComponentCode;

public partial class Instance : ComponentBase {
    protected bool isLoaded { get; set; }
    [Inject] private HttpClient _httpClient { get; set; }

    [Parameter] public string InstanceId { get; set; }
    protected Sharenima.Shared.Instance? SelectedInstance { get; set; }

    protected override async Task OnInitializedAsync() {
        HttpResponseMessage httpResponseMessage = await _httpClient.GetAsync($"Instance?instanceName={InstanceId}");

        if (!httpResponseMessage.IsSuccessStatusCode) {
            if (httpResponseMessage.StatusCode == HttpStatusCode.NotFound) {
            }
        }

        Sharenima.Shared.Instance? instance = await httpResponseMessage.Content.ReadFromJsonAsync<Sharenima.Shared.Instance>();
        if (instance == null) return;

        SelectedInstance = instance;
        isLoaded = true;
    }
}