using System.Collections.Concurrent;

namespace PetChat.ApiServer.SignalR.PresenceTracker;

public class InMemoryPresenceTracker() : IPresenceTracker
{
    // userId -> набор активных соединений
    private readonly ConcurrentDictionary<int, HashSet<string>> _connections = new();
    public async Task UserConnected(int userId, string connectionId)
    {
        var set = _connections.GetOrAdd(userId, _ => []);
        lock (set)
        {
            set.Add(connectionId);
        }
    }

    public async Task<bool> UserDisconnected(int userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var set)) return false;

        lock (set)
        {
            set.Remove(connectionId);
            if (set.Count is 0)
            {
                _connections.TryRemove(userId, out _);
                return false;
            }
            return true;
        }
    }

    public bool IsOnline(int userId)
    {
        return _connections.TryGetValue(userId, out var set) && set.Count > 0;
    }
}