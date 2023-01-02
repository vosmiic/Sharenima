using Microsoft.AspNetCore.Authorization;
using Sharenima.Server.Data;
using Sharenima.Shared;

namespace Sharenima.Server.Handlers; 

public class DeleteVideoRequirement : IAuthorizationRequirement {
    public DeleteVideoRequirement(bool deleteVideo) =>
        DeleteVideo = deleteVideo;

    public bool DeleteVideo { get; }
}

public class DeleteVideoHandler : AuthorizationHandler<UploadVideoRequirement> {
    private readonly ApplicationDbContext _context;
    private readonly GeneralDbContext _generalDbContext;

    public DeleteVideoHandler(ApplicationDbContext context, GeneralDbContext generalDbContext) {
        _context = context;
        _generalDbContext = generalDbContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, UploadVideoRequirement requirement) =>
        BaseHandler.BaseAuthorizationHandler(context, requirement, _context, _generalDbContext, Permissions.Permission.DeleteVideo);
}

internal class DeleteVideoAuthorizeAttribute : AuthorizeAttribute {
    const string POLICY_PREFIX = "DeleteVideo";

    public DeleteVideoAuthorizeAttribute(Guid instanceId) => InstanceId = instanceId;

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