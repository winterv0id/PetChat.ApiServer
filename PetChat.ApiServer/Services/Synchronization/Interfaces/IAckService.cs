namespace PetChat.ApiServer.Services.Synchronization.Interfaces;

public interface IAckService
{
    Task WriteAckAsync(int userId, long sequence, CancellationToken ct = default);
}