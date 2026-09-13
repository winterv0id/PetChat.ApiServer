using PetChat.ApiServer.Database.Entities.Userspace;

namespace PetChat.ApiServer.Database.Repositories.Interfaces;

public interface IChatsRepository
{
    Task<Chat> CreateAsync(int userId, int peerId, string chatId, CancellationToken ct = default);
    Task<Chat?> UpdateLastMessageAsync(string chatId, Message lastMessage, CancellationToken ct = default);
    Task<Chat?> GetWithLastMessageAsync(string chatId, CancellationToken ct = default);
    Task<Chat?> GetAsync(string chatId, CancellationToken ct = default);
    Task<bool> UserIsMember(int userId, string chatId, CancellationToken ct = default);
    Task<int> GetNextIndex(string chatId, CancellationToken ct = default);
}