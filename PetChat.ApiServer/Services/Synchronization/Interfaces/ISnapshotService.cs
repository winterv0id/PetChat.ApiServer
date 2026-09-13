using PetChat.ApiServer.Services.Synchronization.Model;

namespace PetChat.ApiServer.Services.Synchronization.Interfaces;

public interface ISnapshotService
{
    Task<(UserSnapshot Snapshot, long Sequence)> GetUserSnapshotAsync(int userId, CancellationToken ct = default);
}