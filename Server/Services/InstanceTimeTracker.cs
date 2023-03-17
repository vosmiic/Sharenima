namespace Sharenima.Server.Services;

public class InstanceTimeTracker {
    private readonly ILogger<InstanceTimeTracker> _logger;

    public InstanceTimeTracker(ILogger<InstanceTimeTracker> logger) {
        _logger = logger;
    }

    private List<(Guid instanceId, TimeSpan instanceTime)> _instanceTimes { get; set; } =
        new();

    /// <summary>
    /// Add instance to list of instance times.
    /// </summary>
    /// <param name="instanceId">Instance ID to add to the list.</param>
    /// <param name="instanceTime">Time of the instance.</param>
    public void Add(Guid instanceId, TimeSpan instanceTime) {
        if (_instanceTimes.FirstOrDefault(instanceTime => instanceTime.instanceId == instanceId).Equals(default))
            _instanceTimes.Add((instanceId, instanceTime));
    }

    /// <summary>
    /// Update instance time.
    /// </summary>
    /// <param name="instanceId">Instance ID to update.</param>
    /// <param name="newInstanceTime">New instance time to set to.</param>
    /// <param name="createNewIfNotExist">True to add the instance to the list if it does not already exist.</param>
    public void Update(Guid instanceId, TimeSpan newInstanceTime, bool createNewIfNotExist = false) {
        (Guid instanceId, TimeSpan instanceTime) instanceTime = _instanceTimes.FirstOrDefault(instanceTime => instanceTime.instanceId == instanceId);
        if (!instanceTime.Equals(default)) {
            lock (_instanceTimes) {
                _instanceTimes.Remove(instanceTime);
                Add(instanceId, newInstanceTime);
            }
        } else if (createNewIfNotExist) {
            Add(instanceId, newInstanceTime);
        }
        _logger.LogTrace($"Update instance {instanceId} time to {newInstanceTime}");
    }

    /// <summary>
    /// Get instance stored time.
    /// </summary>
    /// <param name="instanceId">Instance ID to get the time of.</param>
    /// <returns>Instance time or null if value does not exist.</returns>
    public TimeSpan? GetInstanceTime(Guid instanceId) {
        (Guid instanceId, TimeSpan instanceTime) instanceTime = _instanceTimes.FirstOrDefault(instanceTime => instanceTime.instanceId == instanceId);
        return instanceTime.Equals(default) ? null : instanceTime.instanceTime;
    }
}