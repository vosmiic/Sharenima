using Sharenima.Shared;

namespace Sharenima.Client; 

public class PermissionService {
    private List<Permissions.Permission> UserPermissions { get; set; } = new();
    public event Action PermissionsUpdated;

    public bool CheckIfUserHasPermission(Permissions.Permission permission) {
        return UserPermissions.Contains(permission) || UserPermissions.Contains(Permissions.Permission.Administrator);
    }

    public void AddToUserPermissions(Permissions.Permission permission) {
        UserPermissions.Add(permission);
    }

    public void RemoveFromUserPermissions(Permissions.Permission permission) {
        UserPermissions.Remove(permission);
    }

    public void CallPermissionsUpdated() {
        PermissionsUpdated?.Invoke();
    }
}