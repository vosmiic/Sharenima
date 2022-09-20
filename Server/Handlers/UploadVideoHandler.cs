using Microsoft.AspNetCore.Authorization;
using Sharenima.Server.Data;
using Sharenima.Shared;

namespace Sharenima.Server.Handlers; 

public class UploadVideoRequirement : IAuthorizationRequirement {
    public UploadVideoRequirement(bool addVideo) =>
        AddVideo = addVideo;

    public bool AddVideo { get; }
}

public class UploadVideoHandler : AuthorizationHandler<AdministratorRequirement> {
    private readonly ApplicationDbContext _context;
    private readonly GeneralDbContext _generalDbContext;

    public UploadVideoHandler(ApplicationDbContext context, GeneralDbContext generalDbContext) {
        _context = context;
        _generalDbContext = generalDbContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministratorRequirement requirement) =>
        BaseHandler.BaseAuthorizationHandler(context, requirement, _context, _generalDbContext, Permissions.Permission.UploadVideo);
}

internal class UploadVideoAuthorizeAttribute : AuthorizeAttribute {
    const string POLICY_PREFIX = "UploadVideo";

    public UploadVideoAuthorizeAttribute(Guid instanceId) => InstanceId = instanceId;

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