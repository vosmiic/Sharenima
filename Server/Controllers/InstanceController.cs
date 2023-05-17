using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Sharenima.Server.Data;
using Sharenima.Server.Models;
using Sharenima.Server.Services;
using Sharenima.Shared;

namespace Sharenima.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class InstanceController : ControllerBase {
    private readonly IDbContextFactory<GeneralDbContext> _contextFactory;
    private readonly IDbContextFactory<ApplicationDbContext> _applicationDbContextFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ConnectionMapping _connectionMapping;
    private readonly ILogger<InstanceController> _logger;

    public InstanceController(IDbContextFactory<GeneralDbContext> contextFactory,
        UserManager<ApplicationUser> userManager, ConnectionMapping connectionMapping,
        ILogger<InstanceController> logger, IDbContextFactory<ApplicationDbContext> applicationDbContextFactory) {
        _contextFactory = contextFactory;
        _userManager = userManager;
        _connectionMapping = connectionMapping;
        _logger = logger;
        _applicationDbContextFactory = applicationDbContextFactory;
    }

    /// <summary>
    /// Create an instance for the authenticated user.
    /// </summary>
    /// <param name="instanceName">Name of the new instance.</param>
    /// <returns>Created instance.</returns>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult> CreateInstance(string instanceName) {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return BadRequest("Authenticated user could not be found.");

        await using var context = await _contextFactory.CreateDbContextAsync();
        Instance instance = new Instance {
            Name = instanceName,
            CreateById = Guid.Parse(userId)
        };
        context.Instances.Add(instance);

        await context.SaveChangesAsync();

        _logger.LogInformation($"Instance {instance.Id} created by user {userId}");
        return Ok(instance);
    }

    [HttpGet]
    public async Task<ActionResult> GetInstanceFromName(string instanceName, bool includePermissions) {
        await using var generalDbContext = await _contextFactory.CreateDbContextAsync();
        Instance? instance = await generalDbContext.Instances.Where(instance => instance.Name == instanceName).Include(instance => instance.Permissions).FirstOrDefaultAsync();
        if (includePermissions && instance != null) {
            InstanceWithUserPermissions instanceWithUserPermissions = JsonConvert.DeserializeObject<InstanceWithUserPermissions>(JsonConvert.SerializeObject(instance));
            await using var context = await _applicationDbContextFactory.CreateDbContextAsync();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) {
                instanceWithUserPermissions.UserPermissions = instance.Permissions.Where(ip => ip.AnonymousUser).Select(ip => ip.Permissions).ToList();
            } else {
                instanceWithUserPermissions.UserPermissions = instance.Permissions.Where(ip => ip.AnonymousUser == false).Select(ip => ip.Permissions).ToList();
                var advancedRoles = context.AdvancedRoles.Where(item => item.UserId == userId).Select(item => item.Permission).ToList();
                foreach (Permissions.Permission permission in advancedRoles) {
                    if (!instanceWithUserPermissions.UserPermissions.Contains(permission))
                        instanceWithUserPermissions.UserPermissions.Add(permission);
                }
            }

            return Ok(instanceWithUserPermissions);
        }

        return instance != null ? Ok(instance) : NotFound();
    }

    [HttpGet]
    [Route("users")]
    public ActionResult GetInstanceUsers(Guid instanceId) {
        var instanceConnections = _connectionMapping.GetConnections().FirstOrDefault(item => item.Key == instanceId);
        if (instanceConnections.Equals(new KeyValuePair<Guid, List<ConnectionMapping.InstanceConnection>>())) return new NoContentResult();
        return Ok(instanceConnections.Value.Select(item => item.UserName));
    }
}