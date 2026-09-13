using PetChat.ApiServer.Database.Entities.Serverspace;
using PetChat.ApiServer.Database.Repositories;
using PetChat.ApiServer.Tests.Integration.Fixtures;

namespace PetChat.ApiServer.Tests.Integration;

/// <summary>
/// <see cref="UserEventRepository"/> 
/// Обрезка лога не должна зависеть от того, что делают другие пользователи, 
/// обрезать строго по acknowledgedSequence пользователя или событиям старше maxLifeSpan
/// </summary>

[Collection("Postgres")]
public class UserEventRepositoryTests(PostgresFixture fixture)
{
    private static int _userIdCounter = 200_000;
    private static int NextUserId() => Interlocked.Increment(ref _userIdCounter);

    [Fact]
    public async Task AppendAsync_AssignsIncreasingSequence_ForSameUser()
    {
        await using var db = fixture.CreateContext();
        var sut = new UserEventRepository(db);
        int userId = NextUserId();

        var e1 = await sut.AppendAsync(userId, "chat1", "test", new { });
        var e2 = await sut.AppendAsync(userId, "chat1", "test", new { });
        var e3 = await sut.AppendAsync(userId, "chat1", "test", new { });

        Assert.True(e1.Sequence < e2.Sequence);
        Assert.True(e2.Sequence < e3.Sequence);
    }

    [Fact]
    public async Task AppendManyAsync_WritesIndependentSequencePerUser()
    {
        await using var db = fixture.CreateContext();
        var sut = new UserEventRepository(db);
        int userA = NextUserId(), userB = NextUserId();

        var events = await sut.AppendManyAsync([userA, userB], "chat1", "test", new { });

        Assert.Equal(2, events.Count);
        Assert.Contains(events, e => e.UserId == userA);
        Assert.Contains(events, e => e.UserId == userB);

        Assert.All(events, e => Assert.True(e.Sequence > 0));
    }

    [Fact]
    public async Task GetEventsSinceAsync_ReturnsOnlyNewerEvents_AscendingOrder_WithLimit()
    {
        await using var db = fixture.CreateContext();
        var sut = new UserEventRepository(db);
        int userId = NextUserId();

        var events = new List<UserEvent>();

        for (int i = 0; i < 5; i++)
            events.Add(await sut.AppendAsync(userId, "chat1", "test", new { i }));

        var acknowledgedSequence = events[1].Sequence; //клиент подтвердил получение 2го события
        var page = await sut.GetEventsSinceAsync(userId, acknowledgedSequence, limit: 2);

        // должны вернуться 2 события, начиная со 2го 
        Assert.Equal(2, page.Count);
        Assert.Equal(events[2].Sequence, page[0].Sequence);
        Assert.Equal(events[3].Sequence, page[1].Sequence);
        Assert.True(page[0].Sequence < page[1].Sequence); // по возрастанию
    }

    [Fact]
    public async Task GetEventsSinceAsync_DoesNotReturnOtherUsersEvents()
    {
        await using var db = fixture.CreateContext();
        var sut = new UserEventRepository(db);
        int userA = NextUserId(), userB = NextUserId();

        await sut.AppendAsync(userA, null, "test", new { });
        await sut.AppendAsync(userB, null, "test", new { });

        var page = await sut.GetEventsSinceAsync(userA, sinceSequence: 0, limit: 100);

        // все события принадлежат userA
        Assert.All(page, e => Assert.Equal(userA, e.UserId));
    }

    [Fact]
    public async Task CountEventsSinceAsync_MatchesActualNumberOfNewerEvents()
    {
        await using var db = fixture.CreateContext();
        var sut = new UserEventRepository(db);
        int userId = NextUserId();

        var first = await sut.AppendAsync(userId, null, "test", new { });

        for (int i = 0; i < 4; i++)
            await sut.AppendAsync(userId, null, "test", new { });

        var count = await sut.CountEventsSinceAsync(userId, first.Sequence);

        Assert.Equal(4, count);
    }

    [Fact]
    public async Task GetMinAvailableSequenceAsync_NoEventsForUser_ReturnsZero()
    {
        await using var db = fixture.CreateContext();
        var sut = new UserEventRepository(db);

        var min = await sut.GetMinAvailableSequenceAsync(NextUserId());

        Assert.Equal(0, min);
    }

    [Fact]
    public async Task GetMinAvailableSequenceAsync_ReturnsSequenceOfOldestEvent()
    {
        await using var db = fixture.CreateContext();
        var sut = new UserEventRepository(db);
        int userId = NextUserId();

        var first = await sut.AppendAsync(userId, null, "test", new { });
        await sut.AppendAsync(userId, null, "test", new { });

        var min = await sut.GetMinAvailableSequenceAsync(userId);

        Assert.Equal(first.Sequence, min);
    }

    [Fact]
    public async Task DeteleUserEvents_RemovesOnlyTargetUsersEvents()
    {
        await using var db = fixture.CreateContext();
        var sut = new UserEventRepository(db);
        int userA = NextUserId(), userB = NextUserId();

        await sut.AppendAsync(userA, null, "test", new { });
        await sut.AppendAsync(userB, null, "test", new { });

        await sut.DeteleUserEvents(userA);

        Assert.Equal(0, await sut.CountEventsSinceAsync(userA, 0));
        Assert.Equal(1, await sut.CountEventsSinceAsync(userB, 0));
    }


    [Fact]
    public async Task TrimAcknowledgedOrOldEventsAsync_RemovesEventsBelowAckMargin_KeepsRecent()
    {
        await using var db = fixture.CreateContext();
        var sut = new UserEventRepository(db);
        int userId = NextUserId();

        var events = new List<UserEvent>();

        for (int i = 0; i < 6; i++)
            events.Add(await sut.AppendAsync(userId, null, "test", new { }));

        // Клиент подтвердил последнее событие
        db.UserAckStates.Add(new UserAckState
        {
            UserId = userId,
            AcknowledgedSequence = events[^1].Sequence,
            AcknowledgedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // ackMargin=2, должны выжить события с sequence >= (sequence - 2) - последние 3 из 6 ([3],[4],[5])
        await sut.TrimAcknowledgedOrOldEventsAsync(ackMargin: 2, maxLifeSpan: TimeSpan.FromDays(30));

        var remaining = await sut.GetEventsSinceAsync(userId, sinceSequence: 0, limit: 100);

        Assert.Equal(3, remaining.Count);
        Assert.DoesNotContain(remaining, e => e.Sequence == events[0].Sequence);
        Assert.Contains(remaining, e => e.Sequence == events[^1].Sequence);
    }

    [Fact]
    public async Task TrimAcknowledgedOrOldEventsAsync_UserWithoutAckSeq_ButEventOlderThanMaxLifeSpan_IsRemoved()
    {
        await using var db = fixture.CreateContext();
        var sut = new UserEventRepository(db);
        int userId = NextUserId();

        db.UserEvents.Add(new UserEvent
        {
            UserId = userId,
            EventType = "test",
            PayloadJson = "{}",
            CreatedAt = DateTime.UtcNow - TimeSpan.FromDays(10)
        });
        await db.SaveChangesAsync();

        await sut.TrimAcknowledgedOrOldEventsAsync(ackMargin: 50, maxLifeSpan: TimeSpan.FromDays(7));

        var remaining = await sut.GetEventsSinceAsync(userId, sinceSequence: 0, limit: 100);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task TrimAcknowledgedOrOldEventsAsync_UserWithoutAck_EventBelowMaxLifeSpan_IsKept()
    {
        await using var db = fixture.CreateContext();
        var sut = new UserEventRepository(db);
        int userId = NextUserId();

        // свежее событие, ack которого ещё не пришёл, не должно удаляться потолком по сроку хранения
        await sut.AppendAsync(userId, null, "test", new { });

        await sut.TrimAcknowledgedOrOldEventsAsync(ackMargin: 50, maxLifeSpan: TimeSpan.FromDays(7));

        var remaining = await sut.GetEventsSinceAsync(userId, sinceSequence: 0, limit: 100);
        Assert.Single(remaining);
    }

    [Fact]
    public async Task TrimAcknowledgedOrOldEventsAsync_OneUsersTrim_DoesNotAffectAnotherUsersLog()
    {
        // обрезка событий одного пользователя не должна задеть лог другого (per-user log)
        await using var db = fixture.CreateContext();
        var sut = new UserEventRepository(db);
        int userWithAck = NextUserId(), userWithoutAck = NextUserId();

        var ackedEvents = new List<UserEvent>();

        for (int i = 0; i < 3; i++)
            ackedEvents.Add(await sut.AppendAsync(userWithAck, null, "test", new { }));

        db.UserAckStates.Add(new UserAckState
        {
            UserId = userWithAck,
            AcknowledgedSequence = ackedEvents[^1].Sequence,
            AcknowledgedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await sut.AppendAsync(userWithoutAck, null, "test", new { });

        await sut.TrimAcknowledgedOrOldEventsAsync(ackMargin: 0, maxLifeSpan: TimeSpan.FromDays(30));

        // все события userWithAck должны быть удалены, кроме последнего (>= ack - 0)
        var remainingAcked = await sut.GetEventsSinceAsync(userWithAck, 0, 100);
        Assert.Single(remainingAcked);

        // событие userWithoutAck должно остаться нетронутым
        var remainingUnacked = await sut.GetEventsSinceAsync(userWithoutAck, 0, 100);
        Assert.Single(remainingUnacked);
    }
}
