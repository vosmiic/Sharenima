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
    public (string?, string?) Add(Guid instanceId, string connectionId, int leaderRank, Guid? userId = null, string? userName = null) {
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
                        LeaderRank = leaderRank,
                        CanBecomeLeader = true
                    }
                });
            }
            return (null, connectionId);
        }

        InstanceConnection? existingConnection = instanceConnections.Value.Value.FirstOrDefault(ic => ic.ConnectionId == connectionId && ic.UserId == userId);
        if (existingConnection == null) {
            InstanceConnection newConnection = new InstanceConnection {
                ConnectionId = connectionId,
                UserId = userId,
                UserName = userName,
                IsLeader = instanceConnections.Value.Value.Count == 0 || instanceConnections.Value.Value.Any(connection => connection.IsLeader && connection.LeaderRank < leaderRank),
                LeaderRank = leaderRank,
                CanBecomeLeader = true
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
            return (oldLeader?.ConnectionId, newConnection.IsLeader ? connectionId : null);
        }

        return (null, null);
    }

    public Dictionary<Guid, List<InstanceConnection>> GetConnections() {
        lock (_instanceConnections) {
            return _instanceConnections;
        }
    }

    public InstanceConnection? GetConnectionById(Guid instanceId, string connectionId) => _instanceConnections.FirstOrDefault(connections => connections.Key == instanceId).Value.FirstOrDefault(ic => ic.ConnectionId == connectionId);

    public string? Remove(Guid instanceId, string connectionId, Guid? userId = null) {
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
                        // below needs changing, should be sorted by leader ranking
                        instanceConnections.Value.Value.First().IsLeader = true;
                        return instanceConnections.Value.Value.First().ConnectionId;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Revoke leadership for user.
    /// </summary>
    /// <param name="instanceId">ID of the instance the connection belongs to.</param>
    /// <param name="connectionId">ID of the connection to revoke leadership of.</param>
    /// <param name="disableLeader">True to remove connection from potential leadership pool.</param>
    public void RevokeLeadership(Guid instanceId, string connectionId, bool disableLeader) {
        InstanceConnection? connection = GetConnectionById(instanceId, connectionId);
        if (connection == null) return;
        connection.IsLeader = false;
        Console.WriteLine($"{connectionId} is no longer leader");
        if (disableLeader) {
            connection.CanBecomeLeader = false;
        }

        ReAllocateLeader(instanceId);
    }

    /// <summary>
    /// Allow user to become a leader.
    /// </summary>
    /// <param name="instanceId">ID of the instance the connection belongs to.</param>
    /// <param name="connectionId">ID of the connection to add to the pool of potential leaders.</param>
    /// <param name="attemptToPromote">True to attempt to promote the connection to leader.</param>
    public void EnableLeader(Guid instanceId, string connectionId, bool attemptToPromote) {
        InstanceConnection? connection = GetConnectionById(instanceId, connectionId);
        if (connection == null) return;
        connection.CanBecomeLeader = true;
        if (attemptToPromote) {
            ReAllocateLeader(instanceId);
        }
    }

    private void ReAllocateLeader(Guid instanceId) {
        var instanceConnections = _instanceConnections.FirstOrDefault(item => item.Key == instanceId);
        if (!instanceConnections.Equals(new KeyValuePair<Guid, List<InstanceConnection>>())) {
            var connectionsThatCanBecomeLeader = instanceConnections.Value.Where(item => item.CanBecomeLeader).ToList();
            if (connectionsThatCanBecomeLeader.Count > 0) {
                var newLeader = connectionsThatCanBecomeLeader.MaxBy(item => item.LeaderRank);
                if (newLeader != null && !newLeader.IsLeader) {
                    var oldLeader = instanceConnections.Value.FirstOrDefault(item => item.IsLeader);
                    if (oldLeader != null) {
                        oldLeader.IsLeader = false;
                    }
                    newLeader.IsLeader = true;
                }
            }
        }
    }

    public class InstanceConnection {
        public Guid? UserId { get; set; }
        public string UserName { get; set; }
        public string ConnectionId { get; set; }
        public bool IsLeader { get; set; }
        public int LeaderRank { get; set; }
        public bool CanBecomeLeader { get; set; }
    }
}