using PetChat.ApiServer.Database.Entities.Userspace;

namespace PetChat.ApiServer.Services.DomainServices.Interfaces;

public interface IMessagesService
{
    Task<Message> SendAsync(int fromId, int peerId, string? text, CancellationToken ct = default);

    Task<bool> MarkReadUpToAsync(int upToMessageId, int readerId, CancellationToken ct = default);

    Task<bool> EditMessage(int ownerId, int messageId, string newText, CancellationToken ct = default);
    
    Task<bool> DeleteMessage(int ownerId, int messageId, CancellationToken ct = default);
}