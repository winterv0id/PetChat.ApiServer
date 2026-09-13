namespace PetChat.ApiServer.Services.DomainServices.Interfaces;

public interface IPresenceEventService
{
    Task SendOfflineAsync(int userId, CancellationToken ct = default);
    Task SendOnlineAsync(int userId, CancellationToken ct = default);
}