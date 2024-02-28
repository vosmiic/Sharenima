namespace Sharenima.Server.Helpers; 

public class RedisHelper {
    public static string InstanceStateChangeKey(Guid instanceId) => $"instance:{instanceId}:statechangelock";
    public static string InstanceVideoTimeKey(Guid instanceId) => $"instance:{instanceId}:videotime";
}