using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Data;
using Sharenima.Server.Models;
using Sharenima.Shared;

namespace Sharenima.Server.Handlers;

public class ChangeProgressRequirement : IAuthorizationRequirement {
    public ChangeProgressRequirement(bool changeProgress) =>
        ChangeProgress = changeProgress;

    public bool ChangeProgress { get; }
}

public class ChangeProgressHandler : AuthorizationHandler<AdministratorRequirement> {
    private readonly ApplicationDbContext _context;

    public ChangeProgressHandler(ApplicationDbContext context) {
        _context = context;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministratorRequirement requirement) => 
        BaseHandler.BaseAuthorizationHandler(context, requirement, _context, Permissions.Permission.ChangeProgress);
}

internal class ChangeProgressAuthorizeAttribute : AuthorizeAttribute {
    const string POLICY_PREFIX = "Admin";

    public ChangeProgressAuthorizeAttribute(Guid instanceId) => InstanceId = instanceId;

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