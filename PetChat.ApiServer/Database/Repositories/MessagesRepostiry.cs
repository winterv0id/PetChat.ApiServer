using Microsoft.EntityFrameworkCore;
using PetChat.ApiServer.Database.Repositories.Interfaces;
using PetChat.ApiServer.Database.Entities.Userspace;

namespace PetChat.ApiServer.Database.Repositories;

public class MessagesRepository(PetChatDbContext petChatDb, IChatsRepository chats) : IMessagesRepository
{
    private readonly PetChatDbContext _petChatDb = petChatDb;
    private readonly IChatsRepository _chats = chats;

    public async Task<Message> SaveAsync(int fromId, int peerId, string? text, string chatId, CancellationToken ct = default)
    {
        int index = await _chats.GetNextIndex(chatId, ct);

        var message = new Message()
        {
            FromId = fromId,
            PeerId = peerId,
            Text = text,
            ChatId = chatId,
            Index = index,
        };

        await _petChatDb.Messages.AddAsync(message, ct);
        await _petChatDb.SaveChangesAsync(ct);
        
        return message;
    }

    public async Task<Message?> GetMessageAsync(int id, CancellationToken ct = default)
    {
        return await _petChatDb.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<int> MarkReadUpToAsync(string chatId, int fromId, int upToIndex, DateTime readDate, CancellationToken ct = default)
    {
        return await _petChatDb.Messages
            .Where(m => m.ChatId == chatId && m.FromId == fromId && !m.Readed && m.Index <= upToIndex)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Readed, true)
                .SetProperty(m => m.ReadDate, readDate), ct);
    }

    public async Task<int> EditMessage(int messageId, string newText, DateTime editDate, CancellationToken ct = default)
    {
        return await _petChatDb.Messages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(s => {
                s.SetProperty(m => m.Text, newText);
                s.SetProperty(m => m.EditDate, editDate);
                s.SetProperty(m => m.Edited, true);
            }, ct);
    }

    public async Task<int> DeleteMessage(int messageId, CancellationToken ct = default)
    {
        return await _petChatDb.Messages
            .Where(m => m.Id == messageId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<Message[]> GetMessagesBeforeAsync(string chatId, int beforeIndex, int limit, CancellationToken ct = default)
    {
        return await _petChatDb.Messages
        .Where(m => m.ChatId == chatId && m.Index < beforeIndex && !m.Deleted)
        .OrderByDescending(m => m.Index)
        .Take(limit)
        .AsNoTracking()
        .ToArrayAsync(ct);
    }
}