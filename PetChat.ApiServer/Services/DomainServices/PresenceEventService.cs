using PetChat.ApiServer.Database.Repositories.Interfaces;
using PetChat.ApiServer.Services.DomainServices.Interfaces;
using PetChat.ApiServer.Services.Synchronization.Interfaces;
using PetChat.ApiServer.Services.Synchronization.Model;

namespace PetChat.ApiServer.Services.DomainServices;

public class PresenceEventService(IUserRelationsRepository userRelations, IUserEventPublisher publisher) : IPresenceEventService
{
    private readonly IUserRelationsRepository _userRelations = userRelations;
    private readonly IUserEventPublisher _publisher = publisher;

    public async Task SendOfflineAsync(int userId, CancellationToken ct = default)
    {
        var related = await _userRelations.GetUserRelations(userId, ct);
        if (related == null) return; //нет связей - некому отправлять

        var payload = new { userId, status = "offline", at = DateTime.UtcNow };
        await _publisher.PublishToManyAsync(related, chatId: null, UserEventType.USER_OFFLINE, payload, ct);
    }

    public async Task SendOnlineAsync(int userId, CancellationToken ct = default)
    {
        var related = await _userRelations.GetUserRelations(userId);
        if (related == null) return; //нет связей - некому отправлять
        
        var payload = new { userId, status = "online", at = DateTime.UtcNow };
        await _publisher.PublishToManyAsync(related, chatId: null, UserEventType.USER_ONLINE, payload, ct);
    }
}