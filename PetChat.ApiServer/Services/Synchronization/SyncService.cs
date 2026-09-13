using PetChat.ApiServer.Database.Repositories.Interfaces;
using PetChat.ApiServer.Services.Synchronization.Interfaces;
using PetChat.ApiServer.Services.Synchronization.Model;

namespace PetChat.ApiServer.Services.Synchronization;

public class SyncService(IUserEventRepository events, ISnapshotService snapshots) : ISyncService
{
    private readonly IUserEventRepository _events = events;
    private readonly ISnapshotService _snapshots = snapshots;

    // количество событий в одном запросе
    private const int LogSize = 500;
    // порог количества событий инкрементального доката
    private const int MaxReasonableIncrementalCatchup = 2000; 

    public async Task<SyncResult> SyncAsync(int userId, long? lastSequence, CancellationToken ct = default)
    {
        if (lastSequence.HasValue)
        {
            //seq самого старого события
            var minAvailable = await _events.GetMinAvailableSequenceAsync(userId, ct);

            // если юзер получал хотя бы одно событие и lastSeq, переданное клиентом было
            // позже или эквивалентно minAvilable, клиент может получить события докаткой, иначе - снапшот
            if (minAvailable > 0 && lastSequence.Value >= minAvailable)
            {
                var missedCount = await _events.CountEventsSinceAsync(userId, lastSequence.Value, ct);

                if (missedCount <= MaxReasonableIncrementalCatchup)
                {
                    var events = await _events.GetEventsSinceAsync(userId, lastSequence.Value, LogSize, ct);
                    var sequence = events.Count > 0 ? events[^1].Sequence : lastSequence.Value;
                    return SyncResult.Incremental(events, hasMore: missedCount > LogSize, sequence);
                }
                // слишком много, лучше сразу снапшот
            }
        }

        //первый вход/разрыв больше окна лога
        var (snapshot, seq) = await _snapshots.GetUserSnapshotAsync(userId, ct);
        return SyncResult.FullSnapshot(snapshot, seq);
    }
}