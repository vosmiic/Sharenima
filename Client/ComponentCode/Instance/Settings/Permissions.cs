using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Reflection;
using MatBlazor;
using Microsoft.AspNetCore.Components;
using Sharenima.Shared;
using Sharenima.Shared.Helpers;

namespace Sharenima.Client.ComponentCode.Settings;

public partial class Permissions : ComponentBase {
    [Parameter] public Guid InstanceId { get; set; }
    [Inject] private HttpClient _httpClient { get; set; }
    [Inject] protected IMatToaster _toaster { get; set; }

    protected InstancePermissions? InstancePermissions { get; set; }
    private UserPermissions? SelectedUser { get; set; }
    protected List<PermissionOptions> PermissionOptions = new();
    protected bool Ready;

    protected override async Task OnInitializedAsync() {
        HttpResponseMessage httpResponseMessage = await _httpClient.GetAsync($"settings/instancePermissions?instanceId={InstanceId}");

        if (httpResponseMessage.IsSuccessStatusCode) {
            InstancePermissions = await httpResponseMessage.Content.ReadFromJsonAsync<InstancePermissions>();
            
            PermissionOptions = Enum.GetValues(typeof(Sharenima.Shared.Permissions.Permission))
                .Cast<Sharenima.Shared.Permissions.Permission>()
                .Select(permission => new PermissionOptions {
                    PermissionEnum = (int)permission,
                    PermissionDisplayName = permission.GetType().GetMember(permission.ToString()).First().GetCustomAttribute<DisplayAttribute>()?.Name,
                    Ticked = InstancePermissions.Permissions?.Any(item => item == permission) ?? false
                })
                .ToList();

            Ready = true;
        }


    }

    protected void ChangeSelectedUser(string selectedUsername) {
        if (string.IsNullOrEmpty(selectedUsername)) {
            SelectedUser = null;
            
            foreach (PermissionOptions permissionOptions in PermissionOptions) {
                permissionOptions.Ticked = InstancePermissions is { Permissions: { } } && InstancePermissions.Permissions.Contains((Sharenima.Shared.Permissions.Permission)permissionOptions.PermissionEnum);
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
        HttpResponseMessage httpResponseMessage = await _httpClient.PostAsync($"settings/userPermissions?instanceId={InstanceId}&user={SelectedUser?.Username ?? "instance"}", JsonConverters.ConvertObjectToHttpContent(PermissionOptions));

        if (httpResponseMessage.IsSuccessStatusCode) {
            _toaster.Add("Permissions saved", MatToastType.Success);
        } else {
            _toaster.Add("Could not save permissions", MatToastType.Danger);
        }

    }
}