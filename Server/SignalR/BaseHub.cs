using Microsoft.AspNetCore.SignalR;

namespace Sharenima.Server.SignalR; 

public class BaseHub : Hub {
    protected Guid? InstanceId {
        get {
            var httpContext = Context.GetHttpContext();
            if (httpContext == null) return null;
            var instanceId = httpContext.Request.Query["instanceId"];
            if (instanceId.Count == 0) return null;
            return Guid.Parse(instanceId[0]);
        }
    }
}