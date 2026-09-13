using Microsoft.AspNetCore.SignalR;
using Moq;
using PetChat.ApiServer.Database.Entities.Serverspace;
using PetChat.ApiServer.Database.Repositories.Interfaces;
using PetChat.ApiServer.Services.Synchronization;
using PetChat.ApiServer.SignalR.Hubs;
using PetChat.ApiServer.SignalR.PresenceTracker;

namespace PetChat.ApiServer.Tests.Unit;

/// <summary>
/// <see cref="UserEventPublisher"/> - запись в лог (БД) происходит всегда,
/// независимо от присутствия пользователя онлайн ("сокет открыт != гарантия доставки")
/// </summary>
public class UserEventPublisherTests
{
    private readonly Mock<IUserEventRepository> _repository = new();
    private readonly Mock<IPresenceTracker> _presence = new();
    private readonly Mock<IHubContext<ChatHub>> _hub = new();
    private readonly Mock<ISingleClientProxy> _clientProxy = new();
    private readonly Mock<IHubClients> _clients = new();
    private readonly UserEventPublisher _sut;

    public UserEventPublisherTests()
    {
        _clients.Setup(c => c.User(It.IsAny<string>())).Returns(_clientProxy.Object);
        _hub.Setup(h => h.Clients).Returns(_clients.Object);
        _sut = new UserEventPublisher(_repository.Object, _hub.Object, _presence.Object);
    }

    [Fact]
    public async Task PublishAsync_UserOffline_StillWritesToRepository()
    {
        _presence.Setup(p => p.IsOnline(1)).Returns(false);
        _repository.Setup(r => r.AppendAsync(
            1, 
            "chat1", 
            "test", 
            It.IsAny<object>(), 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(new UserEvent { 
            UserId = 1, 
            ChatId = "chat1", 
            EventType = "test", 
            PayloadJson = "{}" 
        });

        await _sut.PublishAsync(1, "chat1", "test", new { });

        _repository.Verify(r => r.AppendAsync(
            1, 
            "chat1", 
            "test", 
            It.IsAny<object>(), 
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_UserOffline_DoesNotSendLivePush()
    {
        _presence.Setup(p => p.IsOnline(1)).Returns(false);
        _repository.Setup(r => r.AppendAsync(
            1, 
            "chat1", 
            "test", 
            It.IsAny<object>(), 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(new UserEvent { 
            UserId = 1, 
            ChatId = "chat1", 
            EventType = "test", 
            PayloadJson = "{}" 
        });

        await _sut.PublishAsync(1, "chat1", "test", new { });

        _clients.Verify(c => c.User(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_UserOnline_WritesToRepository_AndSendsLivePush()
    {
        _presence.Setup(p => p.IsOnline(1)).Returns(true);
        _repository.Setup(r => r.AppendAsync(
            1, 
            "chat1", 
            "test", 
            It.IsAny<object>(), 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(new UserEvent { 
            UserId = 1, 
            ChatId = "chat1", 
            EventType = "test", 
            PayloadJson = "{}" 
        });

        await _sut.PublishAsync(1, "chat1", "test", new { });

        _repository.Verify(r => r.AppendAsync(
            1, 
            "chat1", 
            "test", 
            It.IsAny<object>(), 
            It.IsAny<CancellationToken>()
        ), Times.Once);

        _clients.Verify(c => c.User("1"), Times.Once);

        _clientProxy.Verify(p => p.SendCoreAsync(
            "Event", 
            It.IsAny<object[]>(), 
            It.IsAny<CancellationToken>()
        ),Times.Once);
    }

    [Fact]
    public async Task PublishToManyAsync_MixedPresence_OnlyNotifiesOnlineUsers_ButWritesForAll()
    {
        var events = new List<UserEvent>
        {
            new() { UserId = 1, EventType = "test", PayloadJson = "{}" },
            new() { UserId = 2, EventType = "test", PayloadJson = "{}" },
            new() { UserId = 3, EventType = "test", PayloadJson = "{}" }
        };
        _repository.Setup(r => r.AppendManyAsync(
            It.IsAny<IEnumerable<int>>(), 
            It.IsAny<string?>(), 
            "test", 
            It.IsAny<object>(), 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(events);

        _presence.Setup(p => p.IsOnline(1)).Returns(true);
        _presence.Setup(p => p.IsOnline(2)).Returns(false);
        _presence.Setup(p => p.IsOnline(3)).Returns(true);

        await _sut.PublishToManyAsync([1, 2, 3], null, "test", new { });

        _repository.Verify(r => r.AppendManyAsync(
            It.IsAny<IEnumerable<int>>(), 
            It.IsAny<string?>(), 
            "test", 
            It.IsAny<object>(), 
            It.IsAny<CancellationToken>()
        ), Times.Once);

        _clients.Verify(c => c.User("1"), Times.Once);
        _clients.Verify(c => c.User("2"), Times.Never);
        _clients.Verify(c => c.User("3"), Times.Once);
    }

    [Fact]
    public async Task PublishWithoutSavingAsync_NeverTouchesRepository()
    {
        // недолговечные события (typing/stopped_typing) не должны сохраняться в лог
        _presence.Setup(p => p.IsOnline(1)).Returns(false);

        await _sut.PublishWithoutSavingAsync(1, null, "user_typing", new { });

        _repository.Verify(r => r.AppendAsync(
            It.IsAny<int>(), 
            It.IsAny<string?>(), 
            It.IsAny<string>(), 
            It.IsAny<object>(), 
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task PublishWithoutSavingAsync_UserOffline_DoesNotSendAnything()
    {
        _presence.Setup(p => p.IsOnline(1)).Returns(false);

        await _sut.PublishWithoutSavingAsync(1, null, "user_typing", new { });

        _clients.Verify(c => c.User(It.IsAny<string>()), Times.Never);
    }
}