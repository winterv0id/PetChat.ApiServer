using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using PetChat.ApiServer.Database.Entities.Serverspace;
using PetChat.ApiServer.Database.Repositories.Interfaces;
using PetChat.ApiServer.Services.Synchronization.Interfaces;
using PetChat.ApiServer.SignalR.Hubs;
using PetChat.ApiServer.SignalR.PresenceTracker;

namespace PetChat.ApiServer.Services.Synchronization;

public class UserEventPublisher(IUserEventRepository repository, IHubContext<ChatHub> hub, IPresenceTracker presence) : IUserEventPublisher
{
    private readonly IUserEventRepository _repository = repository;
    private readonly IHubContext<ChatHub> _hub = hub;
    private readonly IPresenceTracker _presence = presence;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(int userId, string? chatId, string eventType, object payload, CancellationToken ct = default)
    {
        var evt = await _repository.AppendAsync(userId, chatId, eventType, payload, ct);

        if (_presence.IsOnline(userId))
            await _hub.Clients.User(userId.ToString()).SendAsync("Event", evt, ct);
    }

    public async Task PublishToManyAsync(IEnumerable<int> userIds, string? chatId, string eventType, object payload, CancellationToken ct = default)
    {
        var events = await _repository.AppendManyAsync(userIds, chatId, eventType, payload, ct);

        foreach (var evt in events)
            if (_presence.IsOnline(evt.UserId))
                await _hub.Clients.User(evt.UserId.ToString()).SendAsync("Event", evt, ct);
    }

    public async Task PublishWithoutSavingAsync(int userId, string? chatId, string eventType, object payload, CancellationToken ct = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var evt = new UserEvent(userId, chatId, eventType, payloadJson)
        {
            Sequence = -1
        };

        if (_presence.IsOnline(userId))
            await _hub.Clients.User(userId.ToString()).SendAsync("Event", evt, ct);
    }

    public async Task PublishToManyWithoutSavingAsync(IEnumerable<int> userIds, string? chatId, string eventType, object payload, CancellationToken ct = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var evt = new UserEvent(0, chatId, eventType, payloadJson)
        {
            Sequence = -1
        };

        foreach (int userId in userIds)
        {
            if (_presence.IsOnline(userId))
            {
                evt.UserId = userId;
                await _hub.Clients.User(userId.ToString()).SendAsync("Event", evt, ct);
            }
        }
    }
}