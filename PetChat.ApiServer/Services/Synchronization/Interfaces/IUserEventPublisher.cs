namespace PetChat.ApiServer.Services.Synchronization.Interfaces;

public interface IUserEventPublisher
{
    Task PublishAsync(int userId, string? chatId, string eventType, object payload, CancellationToken ct = default);
    Task PublishToManyAsync(IEnumerable<int> userIds, string? chatId, string eventType, object payload, CancellationToken ct = default);

    Task PublishWithoutSavingAsync(int userId, string? chatId, string eventType, object payload, CancellationToken ct = default);
    Task PublishToManyWithoutSavingAsync(IEnumerable<int> userIds, string? chatId, string eventType, object payload, CancellationToken ct = default);
}