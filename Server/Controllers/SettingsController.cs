using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Data;
using Sharenima.Server.Helpers;
using Sharenima.Server.Models;
using Sharenima.Server.SignalR;
using Sharenima.Shared;

namespace Sharenima.Server.Controllers; 

[ApiController]
[Route("[controller]")]
public class SettingsController : ControllerBase {
    private readonly IDbContextFactory<ApplicationDbContext> _applicationDbContextFactory;
    private readonly IDbContextFactory<GeneralDbContext> _generalDbCotextFactory;
    private readonly IHubContext<QueueHub> _hubContext;
    private readonly ILogger<SettingsController> _logger;
    private readonly ConnectionMapping _connectionMapping;

    public SettingsController(IDbContextFactory<ApplicationDbContext> applicationDbContextFactory, IDbContextFactory<GeneralDbContext> generalDbCotextFactory, ILogger<SettingsController> logger, IHubContext<QueueHub> hubContext, ConnectionMapping connectionMapping) {
        _applicationDbContextFactory = applicationDbContextFactory;
        _generalDbCotextFactory = generalDbCotextFactory;
        _logger = logger;
        _hubContext = hubContext;
        _connectionMapping = connectionMapping;
    }


    [HttpGet]
    [Route("instancePermissions")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> ListInstancePermissions(Guid instanceId) {
        await using var generalContext = await _generalDbCotextFactory.CreateDbContextAsync();
        Instance? instance = await generalContext.Instances.Where(instance => instance.Id == instanceId).Include(instance => instance.Permissions).FirstOrDefaultAsync();
        if (instance == null) return BadRequest("Instance could not be found");
        await using var context = await _applicationDbContextFactory.CreateDbContextAsync();

        return Ok(PermissionHelper.ListInstancePermissions(instance, context.Users.Include(au => au.Roles).ToList()));
    }

    [HttpPost]
    [Route("instancePermissions")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult> SaveInstancePermissions(string user, Guid instanceId, bool anonymousUser, [FromBody] List<PermissionOptions> userPermissionList) {
        await using var generalContext = await _generalDbCotextFactory.CreateDbContextAsync();
        Instance? instance = await generalContext.Instances.Where(instance => instance.Id == instanceId).Include(instance => instance.Permissions).FirstOrDefaultAsync();
        if (instance == null) return BadRequest("Instance could not be found");
        await using var context = await _applicationDbContextFactory.CreateDbContextAsync();

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

            _logger.LogInformation($"Saved instance {instance.Id} instance-level permissions");
            await generalContext.SaveChangesAsync();
        } else {
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

            _logger.LogInformation($"Saved instance {instance.Id} user {applicationUser.Id} permissions");
            await context.SaveChangesAsync();
        }

        if (anonymousUser) {
            KeyValuePair<Guid, List<ConnectionMapping.InstanceConnection>> instanceConnectionMappings = _connectionMapping.GetConnections().FirstOrDefault(item => item.Key == instanceId);
            IEnumerable<ConnectionMapping.InstanceConnection> anonUsers = instanceConnectionMappings.Value.Where(item => item.UserId == null);
            foreach (ConnectionMapping.InstanceConnection instanceConnection in anonUsers) {
                foreach (PermissionOptions permissionOptions in userPermissionList) {
                    await _hubContext.Clients.Client(instanceConnection.ConnectionId).SendAsync("PermissionUpdate", (Permissions.Permission)permissionOptions.PermissionEnum, permissionOptions.Ticked);
                }
            }
        } else if (user == "instance") {
            KeyValuePair<Guid, List<ConnectionMapping.InstanceConnection>> instanceConnectionMappings = _connectionMapping.GetConnections().FirstOrDefault(item => item.Key == instanceId);
            IEnumerable<ConnectionMapping.InstanceConnection> authedUsers = instanceConnectionMappings.Value.Where(item => item.UserId != null);
            foreach (ConnectionMapping.InstanceConnection instanceConnection in authedUsers) {
                foreach (PermissionOptions permissionOptions in userPermissionList) {
                    await _hubContext.Clients.Client(instanceConnection.ConnectionId).SendAsync("PermissionUpdate", (Permissions.Permission)permissionOptions.PermissionEnum, permissionOptions.Ticked);
                }
            }
        } else {
            KeyValuePair<Guid, List<ConnectionMapping.InstanceConnection>> instanceConnectionMappings = _connectionMapping.GetConnections().FirstOrDefault(item => item.Key == instanceId);
            var selectedUser = instanceConnectionMappings.Value.FirstOrDefault(item => item.UserName == user);
            if (selectedUser == null) return Ok();
            foreach (PermissionOptions permissionOptions in userPermissionList) {
                await _hubContext.Clients.Client(selectedUser.ConnectionId).SendAsync("PermissionUpdate", (Permissions.Permission)permissionOptions.PermissionEnum, permissionOptions.Ticked);
            }
        }
        
        return Ok();
    }
}