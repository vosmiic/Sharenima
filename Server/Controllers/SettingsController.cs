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
    [Route("userPermissions")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> ListUserPermissions(string instanceName) {
        await using var generalContext = await _generalDbCotextFactory.CreateDbContextAsync();
        Instance? instance = await generalContext.Instances.FirstOrDefaultAsync(instance => instance.Name == instanceName);
        if (instance == null) return BadRequest("Instance could not be found");
        await using var context = await _applicationDbContextFactory.CreateDbContextAsync();
        List<ApplicationUser> users = context.Users.Include(au => au.Roles).ToList();
        List<LimitedUser> limitedUsers = new List<LimitedUser>();
        foreach (ApplicationUser applicationUser in users) {
            limitedUsers.Add(new LimitedUser {
                Username = applicationUser.UserName,
                Permissions = applicationUser.Roles.Where(role => role.InstanceId == instance.Id).Select(item => item.Permission).ToList()
            });
        }

        return Ok(limitedUsers);
    }

    [HttpPost]
    [Route("userPermissions")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> SaveUserPermissions(string user, string instanceName, [FromBody] List<PermissionOptions> userPermissionList) {
        await using var generalContext = await _generalDbCotextFactory.CreateDbContextAsync();
        Instance? instance = await generalContext.Instances.FirstOrDefaultAsync(instance => instance.Name == instanceName);
        if (instance == null) return BadRequest("Instance could not be found");
        
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
        return Ok();
    }
}