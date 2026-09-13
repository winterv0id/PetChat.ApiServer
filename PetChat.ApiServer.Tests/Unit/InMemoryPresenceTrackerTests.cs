using PetChat.ApiServer.SignalR.PresenceTracker;

namespace PetChat.ApiServer.Tests.Unit;

/// <summary>
/// <see cref="InMemoryPresenceTracker"/>
/// тесты многодевайсности -один и тот же userId может
/// держать несколько параллельных ConnectionId,
/// и обязан считаться online, пока жив хотя бы один из них
/// </summary>
public class InMemoryPresenceTrackerTests
{
    private readonly InMemoryPresenceTracker _sut = new();

    [Fact]
    public void IsOnline_NoConnections_ReturnsFalse()
    {
        Assert.False(_sut.IsOnline(userId: 1));
    }

    [Fact]
    public async Task UserConnected_SetUserOnline()
    {
        await _sut.UserConnected(1, "conn-A");

        Assert.True(_sut.IsOnline(1));
    }

    [Fact]
    public async Task UserConnected_TwiceWithDifferentConnections_StillOnline()
    {
        // два устройства одного пользователя одновременно
        await _sut.UserConnected(1, "conn-A");
        await _sut.UserConnected(1, "conn-B");

        Assert.True(_sut.IsOnline(1));
    }

    [Fact]
    public async Task UserDisconnected_OneOfTwoConnections_StillOnline()
    {
        // разрыв сессии, пользователь остаётся online, пока жива хотя бы одна другая сессия.
        await _sut.UserConnected(1, "conn-A");
        await _sut.UserConnected(1, "conn-B");

        var stillOnline = await _sut.UserDisconnected(1, "conn-A");

        Assert.True(stillOnline);
        Assert.True(_sut.IsOnline(1));
    }

    [Fact]
    public async Task UserDisconnected_LastConnection_SetOffline()
    {
        await _sut.UserConnected(1, "conn-A");

        var stillOnline = await _sut.UserDisconnected(1, "conn-A");

        Assert.False(stillOnline);
        Assert.False(_sut.IsOnline(1));
    }

    [Fact]
    public async Task UserDisconnected_UnknownUser_ReturnsFalse_DoesNotThrow()
    {
        // отключение пользователя, который никогда не подключался 
        // (или уже ранее отключался) - не должно бросать исключение
        var result = await _sut.UserDisconnected(userId: 999, connectionId: "conn-X");

        Assert.False(result);
    }

    [Fact]
    public async Task UserDisconnected_UnknownConnectionId_DoesNotAffectOtherConnections()
    {
        await _sut.UserConnected(1, "conn-A");

        // попытка отключить несуществующий connectionId у существующего юзера
        // не должна затронуть его реальное соединение.
        await _sut.UserDisconnected(1, "conn-DOES-NOT-EXIST");

        Assert.True(_sut.IsOnline(1));
    }

    [Fact]
    public async Task DifferentUsers_AreTrackedIndependently()
    {
        await _sut.UserConnected(1, "conn-A");

        Assert.True(_sut.IsOnline(1));
        Assert.False(_sut.IsOnline(2));
    }

    [Fact]
    public async Task ManyConcurrentConnectionsAndDisconnections_ForSameUser_LeaveConsistentState()
    {
        // проверка потокобезопасности lock(set) внутри трекера:
        // 50 параллельных подключений, затем 50 параллельных отключений одного
        // и того же userId не должны привести к рассинхрону/исключению -
        // после всех операций пользователь должен оказаться offline.
        var connectTasks = Enumerable.Range(0, 50)
            .Select(i => _sut.UserConnected(1, $"conn-{i}"));
        await Task.WhenAll(connectTasks);

        Assert.True(_sut.IsOnline(1));

        var disconnectTasks = Enumerable.Range(0, 50)
            .Select(i => _sut.UserDisconnected(1, $"conn-{i}"));
        await Task.WhenAll(disconnectTasks);

        Assert.False(_sut.IsOnline(1));
    }
}