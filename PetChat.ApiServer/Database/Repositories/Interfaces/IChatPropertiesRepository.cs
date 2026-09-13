using PetChat.ApiServer.Database.Entities.Userspace;
using PetChat.ApiServer.Database.Objects;

namespace PetChat.ApiServer.Database.Repositories.Interfaces;

public interface IChatPropertiesRepository
{
    Task<ChatProperties> CreateAsync(int userId, int peerId, string chatId, CancellationToken ct = default);
    Task<ChatProperties?> GetAsync(int userId, string chatId, CancellationToken ct = default);
    Task<int> UpdateProperties(int userId, string chatId, ChatEditablePropertiesScope propertiesScope, CancellationToken ct = default);
}