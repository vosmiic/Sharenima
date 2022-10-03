using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Reflection;
using MatBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Sharenima.Shared;
using Sharenima.Shared.Helpers;

namespace Sharenima.Client.ComponentCode.Settings;

public partial class Permissions : ComponentBase {
    [CascadingParameter]
    private Task<AuthenticationState> authenticationStateTask { get; set; }
    [Parameter] public Guid InstanceId { get; set; }
    [Inject] private IHttpClientFactory HttpClientFactory { get; set; }
    [Inject] protected IMatToaster _toaster { get; set; }

    protected InstancePermissions? InstancePermissions { get; set; }
    protected UserPermissions? SelectedUser { get; set; }
    protected List<PermissionOptions> PermissionOptions = new();
    private bool AnonymousUser { get; set; }
    protected bool Ready { get; set; }
    private HttpClient? _httpClient { get; set; }

    protected override async Task OnInitializedAsync() {
        var authState = await authenticationStateTask;
        if (authState.User.Identity is { IsAuthenticated: true })
            _httpClient = HttpClientFactory.CreateClient("auth");
        if (_httpClient == null) return;
        //todo handle above
        HttpResponseMessage httpResponseMessage = await _httpClient.GetAsync($"settings/instancePermissions?instanceId={InstanceId}");

        if (httpResponseMessage.IsSuccessStatusCode) {
            InstancePermissions = await httpResponseMessage.Content.ReadFromJsonAsync<InstancePermissions>();
            
            PermissionOptions = Enum.GetValues(typeof(Sharenima.Shared.Permissions.Permission))
                .Cast<Sharenima.Shared.Permissions.Permission>()
                .Select(permission => new PermissionOptions {
                    PermissionEnum = (int)permission,
                    PermissionDisplayName = permission.GetType().GetMember(permission.ToString()).First().GetCustomAttribute<DisplayAttribute>()?.Name,
                    Ticked = InstancePermissions.LoggedInUsersPermissions?.Any(item => item == permission) ?? false
                })
                .ToList();

            Ready = true;
        }


    }

    protected void ChangeSelectedUser(string selectedUsername) {
        if (string.IsNullOrEmpty(selectedUsername)) {
            SelectedUser = null;

            if (AnonymousUser) {
                foreach (PermissionOptions permissionOptions in PermissionOptions) {
                    permissionOptions.Ticked = InstancePermissions is { AnonymousUsersPermissions: { } } && InstancePermissions.AnonymousUsersPermissions.Contains((Sharenima.Shared.Permissions.Permission)permissionOptions.PermissionEnum);
                }
            } else {
                foreach (PermissionOptions permissionOptions in PermissionOptions) {
                    permissionOptions.Ticked = InstancePermissions is { LoggedInUsersPermissions: { } } && InstancePermissions.LoggedInUsersPermissions.Contains((Sharenima.Shared.Permissions.Permission)permissionOptions.PermissionEnum);
                }
            }
        }
        if (InstancePermissions?.UserPermissions != null) {
            SelectedUser = InstancePermissions.UserPermissions.FirstOrDefault(user => user.Username == selectedUsername);

            foreach (PermissionOptions permissionOptions in PermissionOptions) {
                permissionOptions.Ticked = SelectedUser.Permissions.Contains((Sharenima.Shared.Permissions.Permission)permissionOptions.PermissionEnum);
            }
        }
    }

    protected async void SavePermissions() {
        if (_httpClient == null) return;
        //todo handle above
        HttpResponseMessage httpResponseMessage = await _httpClient.PostAsync($"settings/instancePermissions?instanceId={InstanceId}&user={SelectedUser?.Username ?? "instance"}&anonymousUser={AnonymousUser}", JsonConverters.ConvertObjectToHttpContent(PermissionOptions));

        if (httpResponseMessage.IsSuccessStatusCode) {
            _toaster.Add("Permissions saved", MatToastType.Success);
        } else {
            _toaster.Add("Could not save permissions", MatToastType.Danger);
        }

    }

    protected void AnonymousLoggedInUserPermissionSwitch(bool toggle) {
        if (toggle) {
            foreach (PermissionOptions permissionOptions in PermissionOptions) {
                permissionOptions.Ticked = InstancePermissions is { AnonymousUsersPermissions: { } } && InstancePermissions.AnonymousUsersPermissions.Contains((Sharenima.Shared.Permissions.Permission)permissionOptions.PermissionEnum);
            }
        } else {
            foreach (PermissionOptions permissionOptions in PermissionOptions) {
                permissionOptions.Ticked = InstancePermissions is { LoggedInUsersPermissions: { } } && InstancePermissions.LoggedInUsersPermissions.Contains((Sharenima.Shared.Permissions.Permission)permissionOptions.PermissionEnum);
            }
        }

        AnonymousUser = toggle;
    }
}