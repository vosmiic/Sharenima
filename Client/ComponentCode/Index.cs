using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Sharenima.Client.ComponentCode; 

public partial class Index : ComponentBase {
    [Inject]
    private HttpClient _httpClient { get; set; }
    protected string InstanceName { get; set; }

    [Authorize]
    protected async Task<bool> CreateInstance() {
        var createInstanceResponse = await _httpClient.PostAsync($"instance?instanceName={InstanceName}", null);
        return createInstanceResponse.IsSuccessStatusCode;
    }
}