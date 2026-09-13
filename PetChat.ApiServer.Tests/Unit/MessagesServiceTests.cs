using Microsoft.Extensions.Logging;
using Moq;
using PetChat.ApiServer.Database.Entities.Userspace;
using PetChat.ApiServer.Database.Repositories.Interfaces;
using PetChat.ApiServer.Services.DomainServices;
using PetChat.ApiServer.Services.Synchronization.Interfaces;

namespace PetChat.ApiServer.Tests.Unit;

/// <summary>
/// <see cref="MessagesService"/> содержит всю авторизационную логику по сообщениям 
/// (кто может редактировать/удалять/отмечать прочитанным).
/// цель проверить условия проверки владения/обладания правами доступа на операвцию
/// </summary>
public class MessagesServiceTests
{
    private readonly Mock<IMessagesRepository> _messages = new();
    private readonly Mock<IChatsRepository> _chats = new();
    private readonly Mock<IChatPropertiesRepository> _chatProperties = new();
    private readonly Mock<IUserRelationsRepository> _userRelations = new();
    private readonly Mock<IUsersRepository> _users = new();
    private readonly Mock<IUserEventPublisher> _publisher = new();
    private readonly Mock<IPushNotificationsService> _push = new();
    private readonly MessagesService _sut;

    public MessagesServiceTests()
    {
        _sut = new MessagesService(
            _messages.Object, 
            _chats.Object, 
            _chatProperties.Object, 
            _userRelations.Object,
            _users.Object, 
            _publisher.Object, 
            _push.Object,
            Mock.Of<ILogger<MessagesService>>()
        );
    }

    #region edit message

    [Fact]
    public async Task EditMessage_Owner_Success()
    {
        var message = MakeMessage(id: 10, fromId: 1, peerId: 2, chatId: "1_2");

        _messages.Setup(m => m.GetMessageAsync(
            10, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(message);
        
        _messages.Setup(m => m.EditMessage(
            10, 
            "новый текст", 
            It.IsAny<DateTime>(), 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(1); // 1 измененная запись

        var result = await _sut.EditMessage(ownerId: 1, messageId: 10, newText: "новый текст");

        Assert.True(result);

        _publisher.Verify(p => p.PublishAsync(
            2, 
            "1_2", 
            It.IsAny<string>(), 
            It.IsAny<object>(), 
            It.IsAny<CancellationToken>()
        ), Times.Once); //вызывался ли userEventPublisher
    }

    [Fact]
    public async Task EditMessage_NotOwner_Failure()
    {
        // Сообщение принадлежит юзеру 1, редактировать пытается юзер 2 (не автор)
        var message = MakeMessage(id: 10, fromId: 1, peerId: 2, chatId: "1_2");

        _messages.Setup(m => m.GetMessageAsync(
            10, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(message);

        var result = await _sut.EditMessage(ownerId: 2, messageId: 10, newText: "подмена текста");

        Assert.False(result);

        // запрос не должен дойти до базы данных
        _messages.Verify(m => m.EditMessage(
            It.IsAny<int>(), 
            It.IsAny<string>(), 
            It.IsAny<DateTime>(), 
            It.IsAny<CancellationToken>()
        ), Times.Never);

        _publisher.Verify(p => p.PublishAsync(
            It.IsAny<int>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<object>(), 
            It.IsAny<CancellationToken>()
        ),Times.Never);
    }

    [Fact]
    public async Task EditMessage_MessageNotFound_ReturnsFalse()
    {
        _messages.Setup(m => m.GetMessageAsync(
            999, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync((Message?)null);

        var result = await _sut.EditMessage(ownerId: 1, messageId: 999, newText: "текст");

        Assert.False(result);
    }

    [Fact]
    public async Task EditMessage_ZeroRowsAffected_ReturnsFalseWithoutPublishing()
    {
        // Состояние гонки: между GetMessageAsync и EditMessage сообщение могло быть удалено
        // параллельно — ExecuteUpdateAsync должен вернуть 0 затронутых строк.
        // Событие в этом случае не должно быть обработано userEventPublisher'ом.
        var message = MakeMessage(id: 10, fromId: 1, peerId: 2, chatId: "1_2");

        _messages.Setup(m => m.GetMessageAsync(
            10, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(message);
        
        _messages.Setup(m => m.EditMessage(
            10, 
            It.IsAny<string>(), 
            It.IsAny<DateTime>(), 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(0);

        var result = await _sut.EditMessage(ownerId: 1, messageId: 10, newText: "текст");

        Assert.False(result);

        _publisher.Verify(p => p.PublishAsync(
            It.IsAny<int>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<object>(), 
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }
    #endregion

    #region Delete message
    [Fact]
    public async Task DeleteMessage_NotOwner_Failure()
    {
        var message = MakeMessage(id: 10, fromId: 1, peerId: 2, chatId: "1_2");
        _messages.Setup(m => m.GetMessageAsync(
            10, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(message);

        var result = await _sut.DeleteMessage(ownerId: 2, messageId: 10);

        Assert.False(result);
        _messages.Verify(m => m.DeleteMessage(
            It.IsAny<int>(), 
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task DeleteMessage_Owner_Success_AndNotifyPeer()
    {
        var message = MakeMessage(id: 10, fromId: 1, peerId: 2, chatId: "1_2");
        _messages.Setup(m => m.GetMessageAsync(
            10, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(message);
        _messages.Setup(m => m.DeleteMessage(
            10, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(1);

        var result = await _sut.DeleteMessage(ownerId: 1, messageId: 10);

        Assert.True(result);
        // Уведомление об удалении отправляется собеседнику (peerId), не самому автору -
        // у автора удаление уже отражено локально по результату invoke.
        _publisher.Verify(p => p.PublishAsync(
            2, 
            "1_2", 
            It.IsAny<string>(), 
            It.IsAny<object>(), 
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }
    #endregion

    #region Mark as readed
    [Fact]
    public async Task MarkAsReaded_ReaderIsAuthor_Failure()
    {
        // Нельзя пометить прочитанным свое сообщение
        // Здесь readerId совпадает с FromId (автор пытается прочитать себя)
        var message = MakeMessage(id: 10, fromId: 1, peerId: 2, chatId: "1_2");
        _messages.Setup(m => m.GetMessageAsync(
            10, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(message);

        var result = await _sut.MarkReadUpToAsync(upToMessageId: 10, readerId: 1);

        Assert.False(result);
        
        _messages.Verify(m => m.MarkReadUpToAsync(
            It.IsAny<string>(), 
            It.IsAny<int>(), 
            It.IsAny<int>(), 
            It.IsAny<DateTime>(), 
            It.IsAny<CancellationToken>()
        ),Times.Never);
    }

    [Fact]
    public async Task MarkAsReaded_ReaderIsNotChatMember_Failure()
    {
        // Сообщение из чата (1,2), но прочитать пытается юзер 3, не участник чата.
        var message = MakeMessage(id: 10, fromId: 1, peerId: 2, chatId: "1_2");
        _messages.Setup(m => m.GetMessageAsync(
            10, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(message);

        var result = await _sut.MarkReadUpToAsync(upToMessageId: 10, readerId: 3);

        Assert.False(result);
    }

    [Fact]
    public async Task MarkAsReaded_Peer_Success_AndNotifyAuthor()
    {
        var message = MakeMessage(id: 10, fromId: 1, peerId: 2, chatId: "1_2");

        _messages.Setup(m => m.GetMessageAsync(
            10, 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(message);
        _messages.Setup(m => m.MarkReadUpToAsync(
            "1_2", 
            1, 
            message.Index, 
            It.IsAny<DateTime>(), 
            It.IsAny<CancellationToken>()
        )).ReturnsAsync(1);

        var result = await _sut.MarkReadUpToAsync(upToMessageId: 10, readerId: 2);

        Assert.True(result);
        // уведомление о прочтении идёт автору (FromId=1), не читателю
        _publisher.Verify(p => p.PublishAsync(
            1, 
            "1_2", 
            It.IsAny<string>(), 
            It.IsAny<object>(), 
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }
    #endregion

    private static Message MakeMessage(int id, int fromId, int peerId, string chatId) => new()
    {
        Id = id,
        FromId = fromId,
        PeerId = peerId,
        ChatId = chatId,
        Index = 1,
        Text = "исходный текст"
    };
}