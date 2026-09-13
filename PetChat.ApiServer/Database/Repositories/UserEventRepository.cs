using Microsoft.EntityFrameworkCore;
using PetChat.ApiServer.Database.Repositories.Interfaces;
using PetChat.ApiServer.Database.Entities.Serverspace;
using System.Text.Json;

namespace PetChat.ApiServer.Database.Repositories;

public class UserEventRepository(PetChatDbContext petChatDb) : IUserEventRepository
{
    private readonly PetChatDbContext _petChatDb = petChatDb;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<UserEvent> AppendAsync(int userId, string? chatId, 
        string eventType, object payload, CancellationToken ct = default)
    {
        var entity = new UserEvent
        {
            UserId = userId, ChatId = chatId, EventType = eventType,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            CreatedAt = DateTime.UtcNow
        };
        await _petChatDb.UserEvents.AddAsync(entity, ct);
        await _petChatDb.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<List<UserEvent>> AppendManyAsync(IEnumerable<int> userIds, 
        string? chatId, string eventType, object payload, CancellationToken ct = default)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);

        var entities = userIds.Select(uid => new UserEvent
        {
            UserId = uid, ChatId = chatId, EventType = eventType,
            PayloadJson = payloadJson
        }).ToList();

        await _petChatDb.UserEvents.AddRangeAsync(entities, ct);
        await _petChatDb.SaveChangesAsync(ct);
        return entities;
    }

    public async Task<List<UserEvent>> GetEventsSinceAsync(int userId, 
        long sinceSequence, int limit, CancellationToken ct = default)
    {
        return await _petChatDb.UserEvents
            .Where(e => e.UserId == userId && e.Sequence > sinceSequence)
            .OrderBy(e => e.Sequence)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(ct);
    } //.AsAsyncEnumerable?
        

    public async Task<long> CountEventsSinceAsync(int userId, long sinceSequence, CancellationToken ct = default)
    {
        // COUNT по индексу
        return await _petChatDb.UserEvents 
            .Where(e => e.UserId == userId && e.Sequence > sinceSequence)
            .LongCountAsync(ct);
    }

    public async Task<long> GetMinAvailableSequenceAsync(int userId, CancellationToken ct = default)
    {
        var min = await _petChatDb.UserEvents
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.Sequence)
            .Select(e => (long?)e.Sequence)
            .FirstOrDefaultAsync(ct);
        return min ?? 0; // 0 = лог пуст, докатка невозможна, нужен снапшот
    }

    public async Task DeteleUserEvents(int userId, CancellationToken ct = default)
    {
        await _petChatDb.UserEvents
            .Where(ue => ue.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task TrimAcknowledgedOrOldEventsAsync(int ackMargin, TimeSpan maxLifeSpan, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - maxLifeSpan;

        // обрезка подтвержденных событий (с запасом ackMargin)
        await _petChatDb.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM user_events ue
            USING users_ack_state uas
            WHERE ue."UserId" = uas."UserId"
              AND (ue."Sequence" < uas."AcknowledgedSequence" - {ackMargin}
                   OR ue."CreatedAt" < {cutoff})
            """, ct);

        // потолок по возрасту событий, чтобы лог не рос бесконечно
        await _petChatDb.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM user_events ue
            WHERE NOT EXISTS (SELECT 1 FROM users_ack_state uas WHERE uas."UserId" = ue."UserId")
              AND ue."CreatedAt" < {cutoff}
            """, ct);
    }
}