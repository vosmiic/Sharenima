using System.Net.Http.Json;
using MatBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;
using Sharenima.Shared;
using Sharenima.Shared.Helpers;
using File = Sharenima.Shared.File;

namespace Sharenima.Client.ComponentCode;

public partial class Queue : ComponentBase {
    [CascadingParameter] private Task<AuthenticationState> authenticationStateTask { get; set; }
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; }
    [Inject] protected IMatToaster _toaster { get; set; }
    [Inject] private NavigationManager _navigationManager { get; set; }
    [Inject] protected QueuePlayerService QueuePlayerService { get; set; }
    [Inject] protected RefreshService RefreshService { get; set; }
    [Inject] private PermissionService PermissionService { get; set; }
    [Parameter] public Guid InstanceId { get; set; }
    [Parameter] public EventCallback<ICollection<Sharenima.Shared.Queue>?> CurrentQueueChanged { get; set; }
    [Parameter] public Player? PlayerSibling { get; set; }
    public string VideoUrl { get; set; }
    [Parameter] public HubConnection? HubConnection { get; set; }
    private HttpClient? _authHttpClient { get; set; }
    private HttpClient _anonymousHttpClient { get; set; }
    protected bool Authenticated { get; set; }

    protected override async Task OnInitializedAsync() {
        var authState = await authenticationStateTask;
        
        if (authState.User.Identity is { IsAuthenticated: true }) {
            _authHttpClient = HttpClientFactory.CreateClient("auth");
        }
        PermissionCheck();
        PermissionService.PermissionsUpdated += PermissionCheck;


        _anonymousHttpClient = HttpClientFactory.CreateClient("anonymous");
        await GetQueue();

        await Hub();
    }

    protected async void AddVideoToQueue() {
        if (_authHttpClient == null) return;
        //todo handle above
        var addVideoResponse = await _authHttpClient.PostAsync($"queue?instanceId={InstanceId}&videoUrl={VideoUrl}", null);

        if (!addVideoResponse.IsSuccessStatusCode) {
            _toaster.Add($"Could not add video; {addVideoResponse.ReasonPhrase}", MatToastType.Danger, "Error");
        }
    }

    protected async void UploadVideoToQueue(IMatFileUploadEntry[] files) {
        if (_authHttpClient == null) return;
        //todo handle above
        foreach (IMatFileUploadEntry matFileUploadEntry in files) {
            if (!matFileUploadEntry.Type.StartsWith("video")) {
                _toaster.Add("File is not a recognised video type", MatToastType.Danger, "Upload Error");
                return;
            }

            var stream = new MemoryStream();
            await matFileUploadEntry.WriteToStreamAsync(stream);
            string base64File = Convert.ToBase64String(stream.ToArray());
            File file = new File {
                fileBase64 = base64File,
                fileName = matFileUploadEntry.Name,
                MediaType = matFileUploadEntry.Type
            };
            var uploadVideoResponse = await _authHttpClient.PostAsync($"queue/fileUpload?instanceId={InstanceId}&connectionId={HubConnection?.ConnectionId}", JsonConverters.ConvertObjectToHttpContent(file));


            if (!uploadVideoResponse.IsSuccessStatusCode) {
                _toaster.Add($"Could not upload video; {uploadVideoResponse.ReasonPhrase}", MatToastType.Danger, "Upload Error");
            }
        }
    }

    protected async void RemoveFromVideoQueue(Guid queueId) {
        if (_authHttpClient == null) return;
        //todo handle above
        var removeVideoResponse = await _authHttpClient.DeleteAsync($"queue?instanceId={InstanceId}&queueId={queueId}");

        if (!removeVideoResponse.IsSuccessStatusCode) {
            _toaster.Add($"Could not remove video; {removeVideoResponse.ReasonPhrase}", MatToastType.Danger, "Error");
        }
    }

    private async Task GetQueue() {
        var httpResponse = await _anonymousHttpClient.GetAsync($"queue?instanceId={InstanceId}");

        if (!httpResponse.IsSuccessStatusCode) {
            _toaster.Add("Could not load video queue", MatToastType.Danger, "Error");
        }

        ICollection<Sharenima.Shared.Queue>? queueCollection = await httpResponse.Content.ReadFromJsonAsync<ICollection<Sharenima.Shared.Queue>>();
        if (queueCollection != null) {
            await CurrentQueueChanged.InvokeAsync(queueCollection);
            QueuePlayerService.SetQueue(queueCollection.ToList());
            if (queueCollection.Count > 0) {
                RefreshService.CallPlayerRefreshRequested();
                RefreshService.CallInstanceIndexRefresh();
            }
        }
    }

    private void PermissionCheck() {
        Authenticated = PermissionService.CheckIfUserHasPermission(Permissions.Permission.DeleteVideo);
        StateHasChanged();
    }

    private async Task Hub() {
        await HubConnection.SendAsync("JoinGroup", InstanceId.ToString());

        HubConnection.On<Sharenima.Shared.Queue>("AnnounceVideo", async (queue) => {
            QueuePlayerService.AddToQueue(queue);
            StateHasChanged();
            if (QueuePlayerService.CurrentQueue.Count == 1)
                RefreshService.CallPlayerRefreshRequested();
        });

        HubConnection.On<Guid>("RemoveVideo", async (queueId) => {
            Sharenima.Shared.Queue? queue = QueuePlayerService.CurrentQueue?.FirstOrDefault(queue => queue.Id == queueId);
            if (queue != null) {
                QueuePlayerService.RemoveFromQueue(queue);
                StateHasChanged();
            }
        });

        HubConnection.On<State>("ReceiveStateChange", (state) => {
            if (state == State.Ended) {
                // remove current video since it has been completed
                Console.WriteLine("Video has ended");
                QueuePlayerService.RemoveFromQueue(QueuePlayerService.CurrentQueue.First());
                RefreshService.CallPlayerVideoEnded();
                RefreshService.CallPlayerRefreshRequested();
                StateHasChanged();
            }
        });
    }
}