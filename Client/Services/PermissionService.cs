using Sharenima.Shared;

namespace Sharenima.Client; 

public class PermissionService {
    private List<Permissions.Permission> UserPermissions { get; set; } = new();
    private List<(Permissions.Permission permission, bool anonymous)> InstancePermissions { get; set; } = new ();
    public event Action PermissionsUpdated;

    public bool CheckIfUserHasPermission(Permissions.Permission permission) {
        return UserPermissions.Contains(permission) || UserPermissions.Contains(Permissions.Permission.Administrator) || InstancePermissions.Exists(p => p.permission == permission && p.anonymous) || InstancePermissions.Exists(p => p is { permission: Permissions.Permission.Administrator, anonymous: true });
    }

    public void AddToUserPermissions(Permissions.Permission permission) {
        UserPermissions.Add(permission);
    }
    
    public void AddToInstancePermissions(Permissions.Permission permission, bool anonymous) {
        InstancePermissions.Add((permission, anonymous));
    }

    public void RemoveFromUserPermissions(Permissions.Permission permission) {
        UserPermissions.Remove(permission);
    }
    
    public void RemoveFromInstancePermissions(Permissions.Permission permission, bool anonymous) {
        InstancePermissions.Remove((permission, anonymous));
    }

    public void CallPermissionsUpdated() {
        PermissionsUpdated?.Invoke();
    }
}