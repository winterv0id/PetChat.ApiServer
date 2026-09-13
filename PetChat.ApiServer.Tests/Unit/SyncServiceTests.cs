using Moq;
using PetChat.ApiServer.Database.Entities.Serverspace;
using PetChat.ApiServer.Database.Repositories.Interfaces;
using PetChat.ApiServer.Services.Synchronization;
using PetChat.ApiServer.Services.Synchronization.Interfaces;
using PetChat.ApiServer.Services.Synchronization.Model;

namespace PetChat.ApiServer.Tests.Unit;

/// <summary>
/// <see cref="SyncService"/> — решает, может ли клиент докатиться по логу событий,
/// или ему нужен полный snapshot (разрыв слишком большой).
/// (<see cref="IUserEventRepository"/> | <see cref="ISnapshotService"/>)
/// Тесты проверяют ветвление 
/// </summary>
public class SyncServiceTests
{
    private readonly Mock<IUserEventRepository> _events = new();
    private readonly Mock<ISnapshotService> _snapshots = new();
    private readonly SyncService _sut;

    public SyncServiceTests()
    {
        _sut = new SyncService(_events.Object, _snapshots.Object);
    }

    [Fact]
    public async Task SyncAsync_FirstConnect_WithoutLastSequence_ReturnsFullSnapshot()
    {
        // Первый вход клиента, lastSequence отсутствует, докатка невозможна
        var snapshot = new UserSnapshot([], []);
        _snapshots.Setup(s => s.GetUserSnapshotAsync(
            1, 
            It.IsAny<CancellationToken>())
        ).ReturnsAsync((snapshot, 42L));

        var result = await _sut.SyncAsync(userId: 1, lastSequence: null);

        Assert.True(result.IsFullSnapshot);
        Assert.Equal(42, result.Sequence);
        Assert.Null(result.Events);

        _events.Verify(e => e.GetEventsSinceAsync(
            It.IsAny<int>(), 
            It.IsAny<long>(), 
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task SyncAsync_LogIsEmptyForUser_ReturnsFullSnapshot()
    {
        // GetMinAvailableSequenceAsync возвращает 0, когда лог для юзера пуст, докатка невозможна
        _events.Setup(e => e.GetMinAvailableSequenceAsync(
            1, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(0L);

        var snapshot = new UserSnapshot([], []);
        _snapshots.Setup(s => s.GetUserSnapshotAsync(
            1, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync((snapshot, 100L));

        var result = await _sut.SyncAsync(userId: 1, lastSequence: 50);

        Assert.True(result.IsFullSnapshot);
        Assert.Equal(100, result.Sequence);
    }

    [Fact]
    public async Task SyncAsync_TrimmedLog_ReturnsFullSnapshot()
    {
        // Сценарий обрезки лога: у юзера в логе события начинаются
        // с sequence=300 (более старые обрезал UserEventRetentionJob), а клиент
        // прислал lastSequence=50 - то есть его разрыв больше, чем окно лога.
        // Докатка невозможна - нужных событий больше нет в user_events.
        _events.Setup(e => e.GetMinAvailableSequenceAsync(
            1, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(300L);

        var snapshot = new UserSnapshot([], []);
        _snapshots.Setup(s => s.GetUserSnapshotAsync(
            1, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync((snapshot, 500L));

        var result = await _sut.SyncAsync(userId: 1, lastSequence: 50);

        Assert.True(result.IsFullSnapshot);
        _events.Verify(e => e.GetEventsSinceAsync(
            It.IsAny<int>(), 
            It.IsAny<long>(), 
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task SyncAsync_LastSequenceEqualsMinAvailable_IsIncrementalNotSnapshot()
    {
        // lastSequence равен minAvailable, не больше него
        // SyncService - (lastSequence.Value >= minAvailable) - докатка должна сработать
        _events.Setup(e => e.GetMinAvailableSequenceAsync(
            1, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(300L);

        _events.Setup(e => e.CountEventsSinceAsync(
            1, 
            300L, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(5L);

        _events.Setup(e => e.GetEventsSinceAsync(
            1, 
            300L, 
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()
            )).ReturnsAsync([MakeEvent(301), MakeEvent(302)]);

        var result = await _sut.SyncAsync(userId: 1, lastSequence: 300);

        Assert.False(result.IsFullSnapshot);

        _snapshots.Verify(s => s.GetUserSnapshotAsync(
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()), 
            Times.Never
        );
    }

    [Fact]
    public async Task SyncAsync_SmallDiff_ReturnsIncrementalWithCorrectSequence()
    {
        // Короткий разрыв, докатка отдаёт недостающие события
        // sequence в ответе должен быть равен sequence последнего события
        _events.Setup(e => e.GetMinAvailableSequenceAsync(
            1, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(10L);
        _events.Setup(e => e.CountEventsSinceAsync(
            1, 
            100L, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(3L);

        var page = new List<UserEvent> { MakeEvent(101), MakeEvent(102), MakeEvent(103) };
        _events.Setup(e => e.GetEventsSinceAsync(
            1, 
            100L, 
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(page);

        var result = await _sut.SyncAsync(userId: 1, lastSequence: 100);

        Assert.False(result.IsFullSnapshot);
        Assert.Equal(103, result.Sequence); // sequence последнего события
        Assert.Equal(3, result.Events!.Count);
    }

    [Fact]
    public async Task SyncAsync_NoNewEvents_ReturnsEmptyIncrementalWithOriginalSequence()
    {
        // Клиент полностью синхронизирован - новых событий нет, 
        // поэтому Sequence должен остаться равным lastSequence запроса
        _events.Setup(e => e.GetMinAvailableSequenceAsync(
            1, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(10L);
        
        _events.Setup(e => e.CountEventsSinceAsync(
            1, 
            100L, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(0L);

        _events.Setup(e => e.GetEventsSinceAsync(
            1, 
            100L, 
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync([]);

        var result = await _sut.SyncAsync(userId: 1, lastSequence: 100);

        Assert.False(result.IsFullSnapshot);
        Assert.Equal(100, result.Sequence);
        Assert.Empty(result.Events!);
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task SyncAsync_TooManyMissedEvents_ReturnsFullSnapshot()
    {
        // Пропущенных событий больше порога MaxReasonableIncrementalCatchup (2000) - 
        // SyncService отдает snapshot 
        _events.Setup(e => e.GetMinAvailableSequenceAsync(
            1, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(10L);
        
        _events.Setup(e => e.CountEventsSinceAsync(
            1, 
            100L, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(2001L); // на 1 больше порога

        var snapshot = new UserSnapshot([], []);
        _snapshots.Setup(s => s.GetUserSnapshotAsync(
            1, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync((snapshot, 5000L));

        var result = await _sut.SyncAsync(userId: 1, lastSequence: 100);

        Assert.True(result.IsFullSnapshot);
        _events.Verify(e => e.GetEventsSinceAsync(
            It.IsAny<int>(), 
            It.IsAny<long>(), 
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task SyncAsync_SeqEqualsThreshold_ReturnIncremental()
    {
        // Ровно MaxReasonableIncrementalCatchup (2000) пропущенных событий - 
        // докатка ещё должна сработать (включительно)
        _events.Setup(e => e.GetMinAvailableSequenceAsync(
            1, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(10L);

        _events.Setup(e => e.CountEventsSinceAsync(
            1, 
            100L, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(2000L);
        
        _events.Setup(e => e.GetEventsSinceAsync(
            1, 
            100L, 
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync([MakeEvent(101)]);

        var result = await _sut.SyncAsync(userId: 1, lastSequence: 100);

        Assert.False(result.IsFullSnapshot);
    }

    [Fact]
    public async Task SyncAsync_EventsCountAbovePage_SetsHasMoreTrue()
    {
        // hasMore = количество пропущенных событий > максимального количества событий за один запрос
        var fullPage = Enumerable.Range(1, 500).Select(i => MakeEvent(50 + i)).ToList();

        _events.Setup(e => e.GetMinAvailableSequenceAsync(
            1, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(10L);
        
        _events.Setup(e => e.CountEventsSinceAsync(
            1, 
            50L, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(1000L);
        
        _events.Setup(e => e.GetEventsSinceAsync(
            1, 
            50L, 
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(fullPage);

        var result = await _sut.SyncAsync(userId: 1, lastSequence: 50);

        Assert.False(result.IsFullSnapshot);
        Assert.True(result.HasMore);
        Assert.Equal(550, result.Sequence); // sequence последнего элемента страницы (50 + 500)
    }

    [Fact]
    public async Task SyncAsync_MinAvailableIsZero_AlwaysReturnsSnapshot()
    {
        // результат GetMinAvailableSequenceAsync, 
        // равый нулю, должен всегда означать, что докатка невозможна
        _events.Setup(e => e.GetMinAvailableSequenceAsync(
            1, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(0L);

        var snapshot = new UserSnapshot([], []);
        _snapshots.Setup(s => s.GetUserSnapshotAsync(
            1, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync((snapshot, 0L));

        var result = await _sut.SyncAsync(userId: 1, lastSequence: 0);

        Assert.True(result.IsFullSnapshot);
    }

    private static UserEvent MakeEvent(long sequence) => new()
    {
        Sequence = sequence,
        UserId = 1,
        EventType = "test_event",
        PayloadJson = "{}"
    };
}
