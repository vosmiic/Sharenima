using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Data;
using Sharenima.Server.Models;
using Sharenima.Shared;

namespace Sharenima.Server.Handlers;

public class BaseHandler {
    public static Task BaseAuthorizationHandler(AuthorizationHandlerContext context, IAuthorizationRequirement requirement, ApplicationDbContext applicationDbContext, GeneralDbContext generalDbContext, Permissions.Permission? additionalPermission = null) {
        string currentUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        ApplicationUser? user = applicationDbContext.Users.Where(user => user.Id == currentUserId).Include(au => au.Roles).FirstOrDefault();
        Guid? instanceId = null;

        switch (context.Resource) {
            case HttpContext httpContext: {
                var queryCollection = httpContext.Request.Query;
                if (queryCollection.ContainsKey("instanceId")) {
                    instanceId = Guid.Parse(queryCollection["instanceId"]);
                }

                break;
            }
            case HubInvocationContext hubInvocationContext: {
                if (Guid.TryParse(hubInvocationContext.HubMethodArguments[0]?.ToString(), out Guid parsedInstanceId)) {
                    instanceId = parsedInstanceId;
                }

                break;
            }
        }

        if (instanceId == null) {
            context.Fail();
            return Task.CompletedTask;
        }

        Instance? instance = generalDbContext.Instances.Where(instance => instance.Id == instanceId).Include(instance => instance.Permissions).FirstOrDefault();
        if (instance == null) {
            context.Fail();
            return Task.CompletedTask;
        }

        if (
            (user != null && user.Roles.Count > 0 &&
             user.Roles.Any(item => item.InstanceId == instanceId.Value && (
                 (item.Permission == Permissions.Permission.Administrator ||
                  (additionalPermission != null &&
                   item.Permission == additionalPermission)
                 )))
            ) ||
            instance.Permissions.Any(perm =>
                perm.Permissions == Permissions.Permission.Administrator ||
                 (additionalPermission != null &&
                  perm.Permissions == additionalPermission))) {
            context.Succeed(requirement);
        } else {
            context.Fail();
        }

        return Task.CompletedTask;
    }
}