namespace PetChat.ApiServer.SignalR.PresenceTracker;

public interface IPresenceTracker
{
    Task UserConnected(int userId, string connectionId);
    Task<bool> UserDisconnected(int userId, string connectionId);
    bool IsOnline(int userId);
}