using Microsoft.EntityFrameworkCore;
using PetChat.ApiServer.Database.Entities.Userspace;
using PetChat.ApiServer.Database.Repositories.Interfaces;

namespace PetChat.ApiServer.Database.Repositories;

public class ChatsRepository(PetChatDbContext petChatDb) : IChatsRepository
{
    private readonly PetChatDbContext _petChatDb = petChatDb;

    public async Task<Chat> CreateAsync(int userId, int peerId, string chatId, CancellationToken ct = default)
    {
        var chat = new Chat()
        {
            Id = chatId,
            Members = [ userId, peerId ]
        };
        await _petChatDb.AddAsync(chat, ct);
        await _petChatDb.SaveChangesAsync(ct);

        return chat;
    }  

    public async Task<Chat?> GetAsync(string chatId, CancellationToken ct = default)
    {
        return await _petChatDb.Chats
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == chatId, ct);
    }

    public async Task<Chat?> GetWithLastMessageAsync(string chatId, CancellationToken ct = default)
    {
        var chat = await _petChatDb.Chats
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == chatId, ct);
            
        if (chat is null) return null;

        chat.LastMessage = await _petChatDb.Messages
            .AsNoTracking()
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.Index)
            .FirstOrDefaultAsync(ct);

        return chat;
    }

    public async Task<Chat?> UpdateLastMessageAsync(string chatId, Message lastMessage, CancellationToken ct = default)
    {
        var chat = await _petChatDb.Chats.FindAsync(chatId, ct);
        if (chat is null) return null;

        chat.LastMessage = lastMessage;
        chat.LastActivityAt = DateTime.UtcNow;
        await _petChatDb.SaveChangesAsync(ct);
        return chat;
    }

    public async Task<int> GetNextIndex(string chatId, CancellationToken ct = default)
    {
        var chat = await _petChatDb.Chats.FindAsync(chatId, ct);
        if (chat is null) return 0;

        chat.MessageIndex++;

        await _petChatDb.SaveChangesAsync(ct);
        return chat.MessageIndex;
    }

    public async Task<bool> UserIsMember(int userId, string chatId, CancellationToken ct = default)
    {
        return await _petChatDb.Chats
            .Where(c => c.Id == chatId)
            .Select(c => c.Members.Contains(userId))
            .FirstOrDefaultAsync();
    }
}