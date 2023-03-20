namespace Sharenima.Server.Services;

public class ConnectionMapping {
    private readonly Dictionary<Guid, List<InstanceConnection>> _instanceConnections = 
        new();

    public int Count(Guid instanceId) {
        return _instanceConnections.TryGetValue(instanceId, out List<InstanceConnection> connections) ? connections.Count : 0;
    }

    /// <summary>
    /// Adds the user to the connection mapping.
    /// </summary>
    /// <param name="instanceId">Instance ID.</param>
    /// <param name="connectionId">User connection ID.</param>
    /// <param name="leaderRank">Leader rank.</param>
    /// <param name="userId">User ID.</param>
    /// <param name="userName">User name.</param>
    /// <returns>True if the user is the leader.</returns>
    public void Add(Guid instanceId, string connectionId, int leaderRank, Guid? userId = null, string? userName = null) {
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
                        UserName = userName,
                        IsLeader = true,
                        LeaderRank = leaderRank
                    }
                });
            }
            return;
        }

        InstanceConnection? existingConnection = instanceConnections.Value.Value.FirstOrDefault(ic => ic.ConnectionId == connectionId && ic.UserId == userId);
        if (existingConnection == null) {
            InstanceConnection newConnection = new InstanceConnection {
                ConnectionId = connectionId,
                UserId = userId,
                UserName = userName,
                IsLeader = instanceConnections.Value.Value.Count == 0 || instanceConnections.Value.Value.Any(connection => connection.IsLeader && connection.LeaderRank < leaderRank),
                LeaderRank = leaderRank
            };
            InstanceConnection? oldLeader = null;
            if (newConnection.IsLeader) {
                oldLeader = instanceConnections.Value.Value.FirstOrDefault(connection => connection.IsLeader);
            }
            lock (_instanceConnections) {
                if (oldLeader != null) {
                    instanceConnections.Value.Value.Remove(oldLeader);
                    oldLeader.IsLeader = false;
                    instanceConnections.Value.Value.Add(oldLeader);
                }
                instanceConnections.Value.Value.Add(newConnection);
            }
            return;
        }
    }

    public Dictionary<Guid, List<InstanceConnection>> GetConnections() {
        lock (_instanceConnections) {
            return _instanceConnections;
        }
    }

    public InstanceConnection? GetConnectionById(Guid instanceId, string connectionId) => _instanceConnections.FirstOrDefault(connections => connections.Key == instanceId).Value.FirstOrDefault(ic => ic.ConnectionId == connectionId);

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
                    if (instanceConnection.IsLeader && instanceConnections.Value.Value.Count > 0) {
                        instanceConnections.Value.Value.First().IsLeader = true;
                    }
                }
            }
        }
    }

    public class InstanceConnection {
        public Guid? UserId { get; set; }
        public string? UserName { get; set; }
        public string ConnectionId { get; set; }
        public bool IsLeader {get;set;}
        public int LeaderRank { get; set; }
    }
}