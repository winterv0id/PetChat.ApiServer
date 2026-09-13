using Microsoft.EntityFrameworkCore;
using PetChat.ApiServer.Database.Entities.Userspace;
using PetChat.ApiServer.Database.Objects;
using PetChat.ApiServer.Database.Repositories.Interfaces;

namespace PetChat.ApiServer.Database.Repositories;

public class ChatPropertiesRepository(PetChatDbContext petChatDb,  IUsersRepository users) : IChatPropertiesRepository
{
    private readonly PetChatDbContext _petChatDb = petChatDb;
    private readonly IUsersRepository _users = users;

    public async Task<ChatProperties> CreateAsync(int userId, int peerId, string chatId, CancellationToken ct = default)
    {
        var chatProperties = new ChatProperties()
        {
            UserId = userId,
            PeerId = peerId,
            Peer = await _users.GetUserAsync(peerId),
            ChatId = chatId
        };
        await _petChatDb.ChatProperties.AddAsync(chatProperties, ct);
        await _petChatDb.SaveChangesAsync(ct);
        return chatProperties;
    }

    public async Task<ChatProperties?> GetAsync(int userId, string chatId, CancellationToken ct = default)
    {
        return await _petChatDb.ChatProperties
            .AsNoTracking()
            .FirstOrDefaultAsync(cp => cp.UserId == userId && cp.ChatId == chatId, ct);
    }

    public async Task<int> UpdateProperties(int userId, string chatId, ChatEditablePropertiesScope propertiesScope, CancellationToken ct = default)
    {
        return await _petChatDb.ChatProperties
            .Where(cp => cp.UserId == userId && cp.ChatId == chatId)
            .ExecuteUpdateAsync(s => {
                if (propertiesScope.IsArchived != null) 
                    s.SetProperty(cp => cp.Archived, propertiesScope.IsArchived);

                if (propertiesScope.IsNotificationsEnabled != null)
                    s.SetProperty(cp => cp.NotificationsEnabled, propertiesScope.IsNotificationsEnabled);

                if (propertiesScope.IsPinned != null)
                    s.SetProperty(cp => cp.Pinned, propertiesScope.IsPinned);
            }, ct);
    }
}