using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Data;
using Sharenima.Server.Models;

namespace Sharenima.Server.Handlers;

public class AdministratorRequirement : IAuthorizationRequirement {
    public AdministratorRequirement(bool admin) =>
        Administrator = admin;

    public bool Administrator { get; }
}

public class AdministratorHandler : AuthorizationHandler<AdministratorRequirement> {
    private readonly ApplicationDbContext _context;

    public AdministratorHandler(ApplicationDbContext context) {
        _context = context;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministratorRequirement requirement) {
        string currentUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        ApplicationUser? user = _context.Users.Where(user => user.Id == currentUserId).Include(au => au.Roles).FirstOrDefault();
        Guid? instanceId = null;
        
        if (context.Resource is HttpContext httpContext) {
            var queryCollection = httpContext.Request.Query;
            if (queryCollection.ContainsKey("instanceId")) {
                instanceId = Guid.Parse(queryCollection["instanceId"]);
            }
        }

        if (instanceId == null || user == null) {
            context.Fail();
            return Task.CompletedTask;
        }
        
        if (user.Roles.Count > 0 && 
            user.Roles.Any(item => item.InstanceId == instanceId.Value && item.Permission == Permission.Administrator)) {
            context.Succeed(requirement);
        } else {
            context.Fail();
        }

        return Task.CompletedTask;
    }
}

internal class AdministratorAuthorizeAttribute : AuthorizeAttribute {
    const string POLICY_PREFIX = "Admin";

    public AdministratorAuthorizeAttribute(Guid instanceId) => InstanceId = instanceId;

    // Get or set the Age property by manipulating the underlying Policy property
    public Guid InstanceId
    {
        get
        {
            if (Guid.TryParse(Policy.Substring(POLICY_PREFIX.Length), out var instanceId))
            {
                return instanceId;
            }
            return default(Guid);
        }
        set
        {
            Policy = $"{POLICY_PREFIX}{value.ToString()}";
        }
    }
}