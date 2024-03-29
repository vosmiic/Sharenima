using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blazored.LocalStorage;
using MatBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
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
    [Inject] protected HubService HubService { get; set; }
    [Inject] private PermissionService PermissionService { get; set; }
    [Inject] private ILocalStorageService _localStorageService { get; set; }
    [Inject] private IJSRuntime _jsRuntime { get; set; }
    [Parameter] public Guid InstanceId { get; set; }
    [Parameter] public EventCallback<ICollection<Sharenima.Shared.Queue.Queue>?> CurrentQueueChanged { get; set; }
    [Parameter] public Player? PlayerSibling { get; set; }
    public string VideoUrl { get; set; }
    [Parameter] public HubConnection? HubConnection { get; set; }
    private HttpClient? _authHttpClient { get; set; }
    private HttpClient? _uploadHttpClient { get; set; }
    private HttpClient _anonymousHttpClient { get; set; }
    protected bool Authenticated { get; set; }
    protected List<Sharenima.Shared.Queue.Queue> QueueList { get; set; } = new List<Sharenima.Shared.Queue.Queue>();
    private List<Sharenima.Shared.Queue.Queue> QueueListOriginal { get; set; } = new List<Sharenima.Shared.Queue.Queue>();
    protected bool AdvancedUploadSettingsIsOpen = false;
    protected UploadAdvancedSettings _uploadAdvancedSettings { get; set; } = new UploadAdvancedSettings();

    protected override async Task OnInitializedAsync() {
        var authState = await authenticationStateTask;

        if (authState.User.Identity is { IsAuthenticated: true }) {
            _authHttpClient = HttpClientFactory.CreateClient("auth");
        }

        if ((await _localStorageService.GetItemAsync<bool?>(nameof(UploadAdvancedSettings.KeepSubtitles))).HasValue)
            _uploadAdvancedSettings.KeepSubtitles = await _localStorageService.GetItemAsync<bool>(nameof(UploadAdvancedSettings.KeepSubtitles));

        PermissionCheck();
        PermissionService.PermissionsUpdated += PermissionCheck;


        _anonymousHttpClient = HttpClientFactory.CreateClient("anonymous");
        await GetQueue();

        await Hub();
    }

    protected override void OnParametersSet() => SetList();

    protected async void AddVideoToQueue() {
        HttpResponseMessage addVideoResponse;
        if (PermissionService.CheckIfUserHasPermission(Permissions.Permission.AddVideo)) {
            string url = $"queue?instanceId={InstanceId}&videoUrl={VideoUrl}";
            if (_authHttpClient != null) {
                addVideoResponse = await _authHttpClient.PostAsync(url, null);
            } else {
                addVideoResponse = await _anonymousHttpClient.PostAsync(url, null);
            }
        } else {
            return;
        }

        if (!addVideoResponse.IsSuccessStatusCode) {
            _toaster.Add($"Could not add video; {addVideoResponse.ReasonPhrase}", MatToastType.Danger, "Error");
        } else {
            VideoUrl = string.Empty;
            StateHasChanged();
        }
    }

    protected async void UploadVideoToQueue(InputFileChangeEventArgs e) {
        if (_uploadHttpClient == null && PermissionService.CheckIfUserHasPermission(Permissions.Permission.AddVideo)) {
            var authState = await authenticationStateTask;
            _uploadHttpClient = HttpClientFactory.CreateClient(authState.User.Identity is { IsAuthenticated: false } ? "anonymous" : "auth");
            _uploadHttpClient.Timeout = TimeSpan.FromHours(1);
        }

        // gives the user and hour to upload a file, any longer and an error is returned
        using var content = new MultipartFormDataContent();
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data");

        try {
            var file = e.File;
            var fileContent = new StreamContent(file.OpenReadStream(Int64.MaxValue));

            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "\"files\"", file.Name);
        } catch (Exception exception) {
            _toaster.Add($"Couldn't upload file: {exception.Message}", MatToastType.Danger, "Upload Error");
            return;
        }

        content.Add(new StringContent(_uploadAdvancedSettings.KeepSubtitles.ToString()), nameof(UploadAdvancedSettings.KeepSubtitles));

        _toaster.Add($"Uploading file...", MatToastType.Info, "Upload");
        var uploadVideoResponse = await _uploadHttpClient.PostAsync($"queue/fileUpload?instanceId={InstanceId}&connectionId={HubConnection?.ConnectionId}", content);
        if (!uploadVideoResponse.IsSuccessStatusCode) {
            _toaster.Add($"Could not upload video; {uploadVideoResponse.ReasonPhrase}", MatToastType.Danger, "Upload Error");
        }
    }

    protected async void RemoveFromVideoQueue(Guid queueId) {
        if (_authHttpClient == null) {
            _toaster.Add("You are not logged in!", MatToastType.Danger, "Error");
            return;
        };
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

        ICollection<Sharenima.Shared.Queue.Queue>? queueCollection = await httpResponse.Content.ReadFromJsonAsync<ICollection<Sharenima.Shared.Queue.Queue>>();
        if (queueCollection != null) {
            await CurrentQueueChanged.InvokeAsync(queueCollection);
            QueuePlayerService.SetQueue(queueCollection.ToList());
            if (queueCollection.Count > 0) {
                await RefreshService.CallPlayerRefreshRequested();
                await RefreshService.CallInstanceIndexRefresh();
            }
        }
    }

    private void PermissionCheck() {
        Authenticated = PermissionService.CheckIfUserHasPermission(Permissions.Permission.DeleteVideo);
        StateHasChanged();
    }

    protected async void OnDrop() {
        if (_authHttpClient == null) return;

        var changeQueuePositionResponse = await _authHttpClient.PostAsync($"queue/order?instanceId={InstanceId}", JsonConverters.ConvertObjectToHttpContent(QueueList));
        if (changeQueuePositionResponse.IsSuccessStatusCode) {
            QueueListOriginal = QueueList;
        } else {
            _toaster.Add("Could not change queue order", MatToastType.Danger, "Error");
            QueueList = QueueListOriginal.OrderBy(queue => queue.Order).ToList();
            StateHasChanged();
        }
    }

    private async Task Hub() {
        await HubConnection.SendAsync("JoinGroup", InstanceId.ToString());

        HubConnection.On<List<Sharenima.Shared.Queue.Queue>>("AnnounceVideo", async (queueList) => {
            QueuePlayerService.SetQueue(queueList);
            SetList();
            StateHasChanged();
            if (QueuePlayerService.CurrentQueue.Count == 1) {
                await RefreshService.CallPlayerRefreshRequested();
            }
        });

        HubConnection.On<List<Sharenima.Shared.Queue.Queue>>("RemoveVideo", async (queueList) => {
            var existingQueue = QueuePlayerService.CurrentQueue;
            QueuePlayerService.SetQueue(queueList);
            var currentQueue = QueuePlayerService.CurrentQueue.FirstOrDefault(item => item.Order == 0);
            if (currentQueue != null && currentQueue.Id != existingQueue.First(queue => queue.Order == 0).Id) {
                // not the same video, so needs changing
                await RunNextVideo();
            } else {
                // same first video so we can just remove it without changing live video
                if (!QueuePlayerService.CurrentQueue.Any()) {
                    await RefreshService.CallPlayerVideoEnded();
                    await RefreshService.CallPlayerRefreshRequested();
                }
                SetList();

                StateHasChanged();
            }
        });

        HubConnection.On<State>("ReceiveStateChange", async (state) => {
            if (state == State.Ended) {
                // remove current video since it has been completed
                QueuePlayerService.RemoveFromQueue(QueuePlayerService.CurrentQueue.First());
                await RunNextVideo();
            }
        });

        HubConnection.On<List<Sharenima.Shared.Queue.Queue>>("QueueOrderChange", (queueList) => {
            QueuePlayerService.SetQueue(queueList);
            SetList();
            StateHasChanged();
        });
        
    }

    private void SetList() {
        QueueList = QueuePlayerService.CurrentQueue.OrderBy(queue => queue.Order).ToList();
        QueueListOriginal = QueueList;
    }

    private async Task RunNextVideo() {
        SetList();
        await RefreshService.CallPlayerVideoEnded();
        await RefreshService.CallPlayerRefreshRequested();
        StateHasChanged();
    }

    protected void AdvancedUploadSettingsButtonClicked() => AdvancedUploadSettingsIsOpen = true;

    protected async void AdvancedSettingChanged(string setting, bool value) {
        switch (setting) {
            case nameof(UploadAdvancedSettings.KeepSubtitles):
                _uploadAdvancedSettings.KeepSubtitles = value;
                break;
        }

        await _localStorageService.SetItemAsync(setting, value);
    }
}