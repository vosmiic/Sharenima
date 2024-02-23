namespace Sharenima.Server.Helpers; 

public class RedisHelper {
    public static string InstanceStateChangeKey(Guid instanceId) => $"instance:{instanceId}:statechangelock";
}