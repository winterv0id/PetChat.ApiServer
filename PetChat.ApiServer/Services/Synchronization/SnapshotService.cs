using System.Data;
using Microsoft.EntityFrameworkCore;
using PetChat.ApiServer.Database;
using PetChat.ApiServer.Services.Synchronization.Interfaces;
using PetChat.ApiServer.Services.Synchronization.Model;

namespace PetChat.ApiServer.Services.Synchronization;

public class SnapshotService(PetChatDbContext petChatDb) : ISnapshotService
{
    private readonly PetChatDbContext _petChatDb = petChatDb;

    public async Task<(UserSnapshot, long)> GetUserSnapshotAsync(int userId, CancellationToken ct = default)
    {
        await using var tx = await _petChatDb.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await _petChatDb.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);

        try
        {
            var chatProperties = await _petChatDb.ChatProperties
                .AsNoTracking()
                .Where(cp => cp.UserId == userId)
                .ToArrayAsync(ct);

            var peerIds = chatProperties.Select(cp => cp.PeerId);
            var users = await _petChatDb.Users
                .AsNoTracking()
                .Where(u => peerIds.Contains(u.Id))
                .ToArrayAsync(ct);

            var chatIds = chatProperties
                .Select(cp => cp.ChatId)
                .ToArray();

            // N самых активных чатов
            var chats = await _petChatDb.Chats
                .Where(c => chatIds.Contains(c.Id))
                .OrderByDescending(c => c.LastActivityAt)
                .Take(50)
                .AsNoTracking()
                .ToArrayAsync(ct);

            foreach (var chat in chats)
            {
                var chatProps = chatProperties.First(cp => cp.ChatId == chat.Id);
                chatProps.Peer = users.First(u => u.Id == chatProps.PeerId);
                chat.ChatProperties = chatProps;
                chat.LastMessage = await _petChatDb.Messages
                    .AsNoTracking()
                    .Where(m => m.ChatId == chat.Id && m.Deleted == false)
                    .OrderByDescending(m => m.Index)
                    .FirstOrDefaultAsync(ct);
            }

            // последние 50 сообщений из каждого чата
            var messages = await _petChatDb.Messages
                .FromSqlInterpolated($@"
                    SELECT * FROM (
                        SELECT m.*, ROW_NUMBER() OVER (PARTITION BY m.""ChatId"" ORDER BY m.""Index"" DESC) AS rn
                        FROM ""messages"" AS m
                        WHERE m.""ChatId"" = ANY({chatIds}) AND m.""Deleted"" = false
                    ) ranked
                    WHERE rn <= 50")
                .AsNoTracking()
                .ToArrayAsync(ct);

            foreach (var message in messages)
                message.FromOwner = message.FromId == userId;

            var sequence = await _petChatDb.UserEvents
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.Sequence)
                .Select(e => (long?)e.Sequence)
                .FirstOrDefaultAsync(ct) ?? 0;

            await tx.CommitAsync(ct);

            return (new UserSnapshot(chats, messages), sequence);
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }
}