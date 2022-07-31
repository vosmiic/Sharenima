using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Data;
using Sharenima.Server.Models;
using Sharenima.Shared;

namespace Sharenima.Server.Controllers; 

[ApiController]
[Route("[controller]")]
public class InstanceController : ControllerBase {
    private readonly IDbContextFactory<GeneralDbContext> _contextFactory;
    private readonly UserManager<ApplicationUser> _userManager;

    public InstanceController(IDbContextFactory<GeneralDbContext> contextFactory,
        UserManager<ApplicationUser> userManager) {
        _contextFactory = contextFactory;
        _userManager = userManager;
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

        return Ok(instance);
    }

    [HttpGet]
    public async Task<ActionResult> GetInstanceFromName(string instanceName) {
        await using var context = await _contextFactory.CreateDbContextAsync();
        Instance? instance = await context.Instances.FirstOrDefaultAsync(instance => instance.Name == instanceName);

        return instance != null ? Ok(instance) : NotFound();
    }
}