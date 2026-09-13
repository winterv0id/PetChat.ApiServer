using PetChat.ApiServer.Database.Entities.Serverspace;

namespace PetChat.ApiServer.Services.Synchronization.Model;

public record SyncResult(bool IsFullSnapshot, List<UserEvent>? Events, bool HasMore, UserSnapshot? Snapshot, long Sequence)
{
    public static SyncResult Incremental(List<UserEvent> events, bool hasMore, long seq) => 
        new(false, events, hasMore, null, seq);
    public static SyncResult FullSnapshot(UserSnapshot snapshot, long seq) => 
        new(true, null, false, snapshot, seq);
}