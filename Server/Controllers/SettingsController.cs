using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Data;
using Sharenima.Server.Models;
using Sharenima.Shared;

namespace Sharenima.Server.Controllers; 

[ApiController]
[Route("[controller]")]
public class SettingsController : ControllerBase {
    private readonly IDbContextFactory<ApplicationDbContext> _applicationDbContextFactory;
    private readonly IDbContextFactory<GeneralDbContext> _generalDbCotextFactory;

    public SettingsController(IDbContextFactory<ApplicationDbContext> applicationDbContextFactory, IDbContextFactory<GeneralDbContext> generalDbCotextFactory) {
        _applicationDbContextFactory = applicationDbContextFactory;
        _generalDbCotextFactory = generalDbCotextFactory;
    }


    [HttpGet]
    [Route("instancePermissions")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> ListInstancePermissions(Guid instanceId) {
        await using var generalContext = await _generalDbCotextFactory.CreateDbContextAsync();
        Instance? instance = await generalContext.Instances.Where(instance => instance.Id == instanceId).Include(instance => instance.Permissions).FirstOrDefaultAsync();
        if (instance == null) return BadRequest("Instance could not be found");
        await using var context = await _applicationDbContextFactory.CreateDbContextAsync();
        List<ApplicationUser> users = context.Users.Include(au => au.Roles).ToList();
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

        return Ok(instancePermissions);
    }

    [HttpPost]
    [Route("instancePermissions")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> SaveInstancePermissions(string user, Guid instanceId, bool anonymousUser, [FromBody] List<PermissionOptions> userPermissionList) {
        await using var generalContext = await _generalDbCotextFactory.CreateDbContextAsync();
        Instance? instance = await generalContext.Instances.Where(instance => instance.Id == instanceId).Include(instance => instance.Permissions).FirstOrDefaultAsync();
        if (instance == null) return BadRequest("Instance could not be found");

        if (user == "instance") {
            foreach (PermissionOptions permissionOptions in userPermissionList) {
                Permissions.Permission permission = (Permissions.Permission)permissionOptions.PermissionEnum;

                if (permissionOptions.Ticked && instance.Permissions.Count(perm => perm.Permissions == permission && perm.AnonymousUser == anonymousUser) == 0) {
                    instance.Permissions.Add(new InstancePermission {
                        Permissions = permission,
                        AnonymousUser = anonymousUser
                    });
                } else if (!permissionOptions.Ticked && instance.Permissions.Count(perm => perm.Permissions == permission && perm.AnonymousUser == anonymousUser) > 0) {
                    InstancePermission? instancePermission = instance.Permissions.FirstOrDefault(perm => perm.Permissions == permission && perm.AnonymousUser == anonymousUser);
                    if (instancePermission != null) {
                        instance.Permissions.Remove(instancePermission);
                    }
                }
            }

            await generalContext.SaveChangesAsync();
        } else {
            await using var context = await _applicationDbContextFactory.CreateDbContextAsync();
            ApplicationUser? applicationUser = await context.Users.Where(applicationUser => applicationUser.UserName == user).Include(au => au.Roles).FirstOrDefaultAsync();
            if (applicationUser == null) return BadRequest("User could not be found");

            foreach (PermissionOptions permissionOptions in userPermissionList) {
                Permissions.Permission permission = (Permissions.Permission)permissionOptions.PermissionEnum;

                if (permissionOptions.Ticked && applicationUser.Roles.Count(role => role.Permission == permission) == 0) {
                    applicationUser.Roles.Add(new AdvancedRole {
                        InstanceId = instance.Id,
                        Permission = permission,
                        UserId = applicationUser.Id
                    });
                } else if (!permissionOptions.Ticked && applicationUser.Roles.Count(role => role.Permission == permission) > 0) {
                    AdvancedRole? role = applicationUser.Roles.FirstOrDefault(ar => ar.Permission == permission);
                    if (role != null)
                        applicationUser.Roles.Remove(role);
                }
            }

            await context.SaveChangesAsync();
        }
        
        return Ok();
    }
}