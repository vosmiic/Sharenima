using Sharenima.Server.Helpers;
using Sharenima.Server.Models;
using Sharenima.Shared;

namespace Sharenima.UnitTests.HelperUnitTests; 

public class PermissionTests {

    [Test]
    public void ReturnCorrectMixedPermissions() {
        Guid instanceId = new Guid();
        Instance instance = new Instance {
            Id = instanceId,
            Permissions = {
                new InstancePermission {
                    AnonymousUser = true,
                    Permissions = Permissions.Permission.Administrator,
                    InstanceId = instanceId
                },
                new InstancePermission {
                    AnonymousUser = true,
                    Permissions = Permissions.Permission.AddVideo,
                    InstanceId = instanceId
                },
                new InstancePermission {
                    AnonymousUser = false,
                    Permissions = Permissions.Permission.Administrator,
                    InstanceId = instanceId
                }
            }
        };
        string userName = "username";
        List<ApplicationUser> applicationUsers = new List<ApplicationUser> {
            new () {
                UserName = userName,
                Roles = {
                    new AdvancedRole {
                        Permission = Permissions.Permission.Administrator,
                        InstanceId = instanceId
                    }
                }
            }
        };

        InstancePermissions instancePermissions = PermissionHelper.ListInstancePermissions(instance, applicationUsers);
        
        Assert.IsTrue(instancePermissions.AnonymousUsersPermissions?.Count == 2);
        Assert.IsTrue(instancePermissions.LoggedInUsersPermissions?.Count == 1);
        Assert.IsTrue(instancePermissions.UserPermissions?.Count == 1);
        Assert.IsTrue(instancePermissions.UserPermissions?.FirstOrDefault()?.Username == userName);
        Assert.IsTrue(instancePermissions.AnonymousUsersPermissions?.Contains(Permissions.Permission.Administrator));
        Assert.IsTrue(instancePermissions.AnonymousUsersPermissions?.Contains(Permissions.Permission.AddVideo));
        Assert.IsTrue(instancePermissions.LoggedInUsersPermissions?.Contains(Permissions.Permission.Administrator));
    }
}