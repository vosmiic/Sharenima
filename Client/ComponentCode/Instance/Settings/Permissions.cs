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

    protected List<LimitedUser>? Users { get; set; }
    protected LimitedUser? SelectedUser { get; set; }
    protected List<PermissionOptions> PermissionOptions = new();

    protected override async Task OnInitializedAsync() {
        HttpResponseMessage httpResponseMessage = await _httpClient.GetAsync($"settings/userPermissions?instanceId={InstanceId}");

        if (httpResponseMessage.IsSuccessStatusCode) {
            Users = await httpResponseMessage.Content.ReadFromJsonAsync<List<LimitedUser>>();
        }

        PermissionOptions = Enum.GetValues(typeof(Sharenima.Shared.Permissions.Permission))
            .Cast<Sharenima.Shared.Permissions.Permission>()
            .Select(permission => new PermissionOptions {
                PermissionEnum = (int)permission,
                PermissionDisplayName = permission.GetType().GetMember(permission.ToString()).First().GetCustomAttribute<DisplayAttribute>()?.Name,
                Ticked = false
            })
            .ToList();
    }

    protected void ChangeSelectedUser(string selectedUsername) {
        if (Users != null) {
            SelectedUser = Users.FirstOrDefault(user => user.Username == selectedUsername);

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