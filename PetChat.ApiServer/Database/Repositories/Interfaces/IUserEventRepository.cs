using PetChat.ApiServer.Database.Entities.Serverspace;

namespace PetChat.ApiServer.Database.Repositories.Interfaces;

public interface IUserEventRepository
{
    Task<UserEvent> AppendAsync(int userId, string? chatId, string eventType, object payload, CancellationToken ct = default);

    // сохранить у множества юзеров
    Task<List<UserEvent>> AppendManyAsync(IEnumerable<int> userIds, string? chatId, string eventType, object payload, CancellationToken ct = default);

    // докатка событий логом
    Task<List<UserEvent>> GetEventsSinceAsync(int userId, long sinceSequence, int limit, CancellationToken ct = default);

    Task<long> CountEventsSinceAsync(int userId, long sinceSequence, CancellationToken ct = default);

    // минимальный доступный sequence в логе пользователя.
    // если lastSequence клиента < этого числа — докатка логом невозможна,
    // нужные события были обрезаны - только snapshot
    Task<long> GetMinAvailableSequenceAsync(int userId, CancellationToken ct = default);

    Task DeteleUserEvents(int userId, CancellationToken ct = default);

    // вызывается из UserEventTrimJob раз в N минут
    Task TrimAcknowledgedOrOldEventsAsync(int ackMargin, TimeSpan maxLifeSpan, CancellationToken ct = default);
}