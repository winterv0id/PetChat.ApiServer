using PetChat.ApiServer.Services.Synchronization.Model;

namespace PetChat.ApiServer.Services.Synchronization.Interfaces;

public interface ISyncService
{
    Task<SyncResult> SyncAsync(int userId, long? lastSequence, CancellationToken ct = default);
}