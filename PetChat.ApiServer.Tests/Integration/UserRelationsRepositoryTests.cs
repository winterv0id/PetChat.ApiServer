using Microsoft.EntityFrameworkCore;
using PetChat.ApiServer.Database.Repositories;
using PetChat.ApiServer.Tests.Integration.Fixtures;

namespace PetChat.ApiServer.Tests.Integration;

/// <summary>
/// <see cref="UserRelationsRepository"/> помогает IPresenceNotificationService 
/// отправлять юзеру статус всех пользователей, с которыми он общается (список связей конкретного пользователя)
/// Каждый новый чат добавляет собеседника в список, поэтому AddOrCreateRelations 
/// должен добавлять новые связи при повторных вызовах,
/// а не только создавать запись с нуля при первом обращении.
/// </summary>
[Collection("Postgres")]
public class UserRelationsRepositoryTests(PostgresFixture fixture)
{
    private static int _userIdCounter = 300_000;
    private static int NextUserId() => Interlocked.Increment(ref _userIdCounter);

    [Fact]
    public async Task GetUserRelations_NoExisting_ReturnsNull()
    {
        await using var db = fixture.CreateContext();
        var repo = new UserRelationsRepository(db);

        var result = await repo.GetUserRelations(NextUserId());

        Assert.Null(result);
    }

    [Fact]
    public async Task AddOrCreateRelations_FirstCall_CreatesEntryWithRelations()
    {
        await using var db = fixture.CreateContext();
        var repo = new UserRelationsRepository(db);
        int userId = NextUserId();

        await repo.AddOrCreateRelations(userId, [10, 20]);

        var result = await repo.GetUserRelations(userId);

        Assert.NotNull(result);
        Assert.Equal([10, 20], result!.OrderBy(x => x));
    }

    [Fact]
    public async Task AddOrCreateRelations_SecondCall_AddNewRelations_KeepsOld()
    {
        await using var db = fixture.CreateContext();
        var repo = new UserRelationsRepository(db);
        int userId = NextUserId();

        await repo.AddOrCreateRelations(userId, [10]);
        await repo.AddOrCreateRelations(userId, [20]);

        var result = await repo.GetUserRelations(userId);

        Assert.NotNull(result);
        Assert.Equal([10, 20], result!.OrderBy(x => x));
    }

    [Fact]
    public async Task AddOrCreateRelations_OverlappingRelations_DoesNotDuplicate()
    {
        await using var db = fixture.CreateContext();
        var repo = new UserRelationsRepository(db);
        int userId = NextUserId();

        await repo.AddOrCreateRelations(userId, [10, 20]);
        await repo.AddOrCreateRelations(userId, [20, 30]); // 20 уже есть

        var result = await repo.GetUserRelations(userId);

        Assert.NotNull(result);
        Assert.Equal([10, 20, 30], result!.OrderBy(x => x));
    }

    [Fact]
    public async Task RemoveRelations_RemovesOnlySpecifiedRelations()
    {
        await using var db = fixture.CreateContext();
        var repo = new UserRelationsRepository(db);
        int userId = NextUserId();

        await repo.AddOrCreateRelations(userId, [10, 20, 30]);
        await repo.RemoveRelations(userId, [20]);

        var result = await repo.GetUserRelations(userId);

        Assert.NotNull(result);
        Assert.Equal([10, 30], result!.OrderBy(x => x));
    }

    [Fact]
    public async Task RemoveRelations_AffectsOnlyTargetUser_NotOtherUsersEntries()
    {
        // операция над одним userId не должна задевать чужие записи
        await using var db = fixture.CreateContext();
        var repo = new UserRelationsRepository(db);
        int userA = NextUserId(), userB = NextUserId();

        await repo.AddOrCreateRelations(userA, [1, 2]);
        await repo.AddOrCreateRelations(userB, [3, 4]);

        await repo.RemoveRelations(userA, [1]);

        var resultA = await repo.GetUserRelations(userA);
        var resultB = await repo.GetUserRelations(userB);

        Assert.Equal([2], resultA!);
        Assert.Equal([3, 4], resultB!.OrderBy(x => x));
    }

    [Fact]
    public async Task RemoveRelations_NoExistingEntry_DoesNotThrow()
    {
        await using var db = fixture.CreateContext();
        var repo = new UserRelationsRepository(db);

        var exception = await Record.ExceptionAsync(() => 
            repo.RemoveRelations(NextUserId(), [1, 2]));

        Assert.Null(exception);
    }
}