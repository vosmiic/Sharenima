using Sharenima.Server.Helpers;
using StackExchange.Redis;

namespace Sharenima.Server.Services;

public class InstanceTimeTracker {
    private readonly ILogger<InstanceTimeTracker> _logger;
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public InstanceTimeTracker(ILoggerFactory loggerFactory,
        IConnectionMultiplexer connectionMultiplexer) {
        _logger = loggerFactory.CreateLogger<InstanceTimeTracker>();
        _connectionMultiplexer = connectionMultiplexer;
    }

    /// <summary>
    /// Add or update instance time.
    /// </summary>
    /// <param name="instanceId">Instance ID that the video time belongs to.</param>
    /// <param name="videoTime">Time of the video to set.</param>
    public void Upsert(Guid instanceId, TimeSpan videoTime) {
        IDatabase database = _connectionMultiplexer.GetDatabase();
        database.SetAdd(RedisHelper.InstanceVideoTimeKey(instanceId), videoTime.TotalMilliseconds);
        _logger.LogInformation($"Set/updated instance {instanceId} video time to {videoTime}.");
    }

    /// <summary>
    /// Delete instance video time.
    /// </summary>
    /// <param name="instanceId">ID of the instance to delete the video time of.</param>
    public bool Remove(Guid instanceId) {
        IDatabase database = _connectionMultiplexer.GetDatabase();
        return database.KeyDelete(RedisHelper.InstanceVideoTimeKey(instanceId));
    }

    /// <summary>
    /// Get instance stored time.
    /// </summary>
    /// <param name="instanceId">Instance ID to get the time of.</param>
    /// <returns>Instance time or null if value does not exist.</returns>
    public TimeSpan? GetInstanceTime(Guid instanceId) {
        IDatabase database = _connectionMultiplexer.GetDatabase();
        RedisValue instanceTime = database.StringGet(RedisHelper.InstanceVideoTimeKey(instanceId));
        return instanceTime.HasValue ? 
            TimeSpan.TryParse(instanceTime, out TimeSpan parsedTime) ?
                parsedTime :
                null :
            null;
    }
}