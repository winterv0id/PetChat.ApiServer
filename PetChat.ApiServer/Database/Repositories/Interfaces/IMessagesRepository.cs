using PetChat.ApiServer.Database.Entities.Userspace;

namespace PetChat.ApiServer.Database.Repositories.Interfaces;

public interface IMessagesRepository
{
    Task<Message> SaveAsync(int fromId, int peerId, string? text, string chatId, CancellationToken ct = default);
    Task<Message?> GetMessageAsync(int id, CancellationToken ct = default);
    Task<int> MarkReadUpToAsync(string chatId, int fromId, int upToIndex, DateTime readDate, CancellationToken ct = default);
    Task<int> EditMessage(int messageId, string newText, DateTime editDate, CancellationToken ct = default);
    Task<int> DeleteMessage(int messageId, CancellationToken ct = default);
    Task<Message[]> GetMessagesBeforeAsync(string chatId, int beforeIndex, int limit, CancellationToken ct = default);
}