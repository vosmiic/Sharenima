using Microsoft.AspNetCore.Authorization;
using Sharenima.Server.Data;

namespace Sharenima.Server.Handlers;

public class AdministratorRequirement : IAuthorizationRequirement {
    public AdministratorRequirement(bool admin) =>
        Administrator = admin;

    public bool Administrator { get; }
}

public class AdministratorHandler : AuthorizationHandler<AdministratorRequirement> {
    private readonly ApplicationDbContext _context;
    private readonly GeneralDbContext _generalDbContext;

    public AdministratorHandler(ApplicationDbContext context, GeneralDbContext generalDbContext) {
        _context = context;
        _generalDbContext = generalDbContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministratorRequirement requirement) =>
        BaseHandler.BaseAuthorizationHandler(context, requirement, _context, _generalDbContext);
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