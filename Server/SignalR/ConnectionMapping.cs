namespace Sharenima.Server.SignalR;

public class ConnectionMapping {
    private readonly Dictionary<Guid, List<InstanceConnection>> _instanceConnections = 
        new();

    public int Count(Guid instanceId) {
        return _instanceConnections.TryGetValue(instanceId, out List<InstanceConnection> connections) ? connections.Count : 0;
    }

    public void Add(Guid instanceId, string connectionId, Guid? userId = null, string? userName = null) {
        KeyValuePair<Guid, List<InstanceConnection>>? instanceConnections;
        lock (_instanceConnections) {
            instanceConnections = _instanceConnections.FirstOrDefault(ic => ic.Key == instanceId);
        }

        if (instanceConnections.Equals(new KeyValuePair<Guid, List<InstanceConnection>>())) {
            lock (_instanceConnections) {
                _instanceConnections.Add(instanceId, new List<InstanceConnection> {
                    new() {
                        ConnectionId = connectionId,
                        UserId = userId,
                        UserName = userName
                    }
                });
            }
        } else if (instanceConnections.Value.Value.FirstOrDefault(ic => ic.ConnectionId == connectionId && ic.UserId == userId) == null) {
            lock (_instanceConnections) {
                instanceConnections.Value.Value.Add(new InstanceConnection {
                    ConnectionId = connectionId,
                    UserId = userId,
                    UserName = userName
                });
            }
        }
    }

    public Dictionary<Guid, List<InstanceConnection>> GetConnections() {
        lock (_instanceConnections) {
            return _instanceConnections;
        }
    }

    public void Remove(Guid instanceId, string connectionId, Guid? userId = null) {
        KeyValuePair<Guid, List<InstanceConnection>>? instanceConnections;
        lock (_instanceConnections) {
            instanceConnections = _instanceConnections.FirstOrDefault(ic => ic.Key == instanceId);
        }
        
        if (instanceConnections != null) {
            var instanceConnection = instanceConnections.Value.Value.FirstOrDefault(ic => ic.ConnectionId == connectionId && ic.UserId == userId);
            if (instanceConnection != null) {
                lock (_instanceConnections) {
                    instanceConnections.Value.Value.Remove(instanceConnection);
                }
            }
        }
    }

    public class InstanceConnection {
        public Guid? UserId { get; set; }
        public string? UserName { get; set; }
        public string ConnectionId { get; set; }
    }
}