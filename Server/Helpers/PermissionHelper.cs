using Sharenima.Server.Models;
using Sharenima.Shared;

namespace Sharenima.Server.Helpers; 

public class PermissionHelper {
    public static InstancePermissions ListInstancePermissions(Instance instance, List<ApplicationUser> users) {
        InstancePermissions instancePermissions = new InstancePermissions();
        instancePermissions.UserPermissions = new List<UserPermissions>();
        if (instance.Permissions.Any()) {
            instancePermissions.LoggedInUsersPermissions = instance.Permissions.Where(instance => !instance.AnonymousUser).Select(instance => instance.Permissions).ToList();
            instancePermissions.AnonymousUsersPermissions = instance.Permissions.Where(instance => instance.AnonymousUser).Select(instance => instance.Permissions).ToList();
        }
        foreach (ApplicationUser applicationUser in users) {
            instancePermissions.UserPermissions.Add(new UserPermissions {
                Username = applicationUser.UserName,
                Permissions = applicationUser.Roles.Where(role => role.InstanceId == instance.Id).Select(item => item.Permission).ToList()
            });
        }

        return instancePermissions;
    }
}