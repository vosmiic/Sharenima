using Microsoft.AspNetCore.Authorization;
using Sharenima.Server.Data;
using Sharenima.Shared;

namespace Sharenima.Server.Handlers; 

public class AddVideoRequirement : IAuthorizationRequirement {
    public AddVideoRequirement(bool addVideo) =>
        AddVideo = addVideo;

    public bool AddVideo { get; }
}

public class AddVideoHandler : AuthorizationHandler<AdministratorRequirement> {
    private readonly ApplicationDbContext _context;

    public AddVideoHandler(ApplicationDbContext context) {
        _context = context;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministratorRequirement requirement) =>
        BaseHandler.BaseAuthorizationHandler(context, requirement, _context, Permissions.Permission.AddVideo);
}

internal class AddVideoAuthorizeAttribute : AuthorizeAttribute {
    const string POLICY_PREFIX = "AddVideo";

    public AddVideoAuthorizeAttribute(Guid instanceId) => InstanceId = instanceId;

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