using Microsoft.EntityFrameworkCore;
using PetChat.ApiServer.Database.Repositories;
using PetChat.ApiServer.Tests.Integration.Fixtures;

namespace PetChat.ApiServer.Tests.Integration;

/// <summary>
/// <see cref="UserAckRepository"/> хранит подтвержденную клиентом позицию, 
/// AcknowledgedSequence не должен откатываться - устаревший/задержавшийся в сети запрос с меньшим 
/// sequence не должен перезаписать более новое уже сохранённое значение. 
/// GREATEST должен гарантировать это поведение.
/// </summary>
 
[Collection("Postgres")]
public class UserAckRepositoryTests(PostgresFixture fixture)
{
    private static int _userIdCounter = 100_000;
    private static int NextUserId() => Interlocked.Increment(ref _userIdCounter);

    [Fact]
    public async Task UpsertIfGreaterAsync_FirstCall_InsertsNewRow()
    {
        await using var db = fixture.CreateContext();
        var repo = new UserAckRepository(db);
        int userId = NextUserId();

        await repo.WriteIfGreaterAsync(userId, 100);

        var state = await db.UserAckStates
            .AsNoTracking()
            .SingleAsync(s => s.UserId == userId);

        Assert.Equal(100, state.AcknowledgedSequence);
    }

    [Fact]
    public async Task UpsertIfGreaterAsync_HigherSequence_UpdatesValue()
    {
        await using var db = fixture.CreateContext();
        var repo = new UserAckRepository(db);
        int userId = NextUserId();

        await repo.WriteIfGreaterAsync(userId, 100);
        await repo.WriteIfGreaterAsync(userId, 250);

        var state = await db.UserAckStates
            .AsNoTracking()
            .SingleAsync(s => s.UserId == userId);
            
        Assert.Equal(250, state.AcknowledgedSequence);
    }

    [Fact]
    public async Task UpsertIfGreaterAsync_LowerSequence_DoesNotRegressStoredValue()
    {
        // Симулирует устаревший/задержавшийся в сети запрос, долетевший после
        // более нового: 250 более новое значение, приходит задержавшееся 100 - 
        // значение должно остаться 250.
        await using var db = fixture.CreateContext();
        var repo = new UserAckRepository(db);
        int userId = NextUserId();

        await repo.WriteIfGreaterAsync(userId, 250);
        await repo.WriteIfGreaterAsync(userId, 100);

        var state = await db.UserAckStates.AsNoTracking().SingleAsync(s => s.UserId == userId);
        Assert.Equal(250, state.AcknowledgedSequence);
    }

    [Fact]
    public async Task UpsertIfGreaterAsync_EqualSequence_NoChanges()
    {
        await using var db = fixture.CreateContext();
        var repo = new UserAckRepository(db);
        int userId = NextUserId();

        await repo.WriteIfGreaterAsync(userId, 100);
        await repo.WriteIfGreaterAsync(userId, 100);

        var state = await db.UserAckStates.AsNoTracking().SingleAsync(s => s.UserId == userId);
        Assert.Equal(100, state.AcknowledgedSequence);
    }

    [Fact]
    public async Task UpsertIfGreaterAsync_ConcurrentCallsWithDifferentValues_ConvergeToMaximum()
    {
        // Гонка параллельных запросов подтверждения - конечное значение должно быть 
        // максимумом из всех присланных значений, независимо от порядка фактического выполнения.
        await using var db = fixture.CreateContext();
        int userId = NextUserId();

        var values = new long[] { 100, 500, 250, 999, 10 };
        var tasks = values.Select(async v =>
        {
            await using var scopedDb = fixture.CreateContext();
            var repo = new UserAckRepository(scopedDb);
            await repo.WriteIfGreaterAsync(userId, v);
        });
        await Task.WhenAll(tasks);

        var state = await db.UserAckStates.AsNoTracking().SingleAsync(s => s.UserId == userId);
        Assert.Equal(999, state.AcknowledgedSequence);
    }
}