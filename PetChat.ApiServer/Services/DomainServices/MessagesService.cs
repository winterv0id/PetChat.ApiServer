using PetChat.ApiServer.Database.Entities.Userspace;
using PetChat.ApiServer.Database.Repositories.Interfaces;
using PetChat.ApiServer.Model;
using PetChat.ApiServer.Services.DomainServices.Interfaces;
using PetChat.ApiServer.Services.Synchronization.Interfaces;
using PetChat.ApiServer.Services.Synchronization.Model;

namespace PetChat.ApiServer.Services.DomainServices;

public class MessagesService(IMessagesRepository messages, IChatsRepository chats,
    IChatPropertiesRepository chatProperties, IUserRelationsRepository userRelations, 
    IUsersRepository users, IUserEventPublisher publisher, IPushNotificationsService pushNotifications, 
    ILogger<MessagesService> logger) : IMessagesService
{
    private readonly IMessagesRepository _messages = messages;
    private readonly IChatsRepository _chats = chats;
    private readonly IChatPropertiesRepository _chatProperties = chatProperties;
    private readonly IUserRelationsRepository _userRelations = userRelations;
    private readonly IUsersRepository _users = users;
    private readonly IUserEventPublisher _publisher = publisher;
    private readonly IPushNotificationsService _pushNotifications = pushNotifications;
    private readonly ILogger<MessagesService> _logger = logger;

    public async Task<Message> SendAsync(int fromId, int peerId, string? text, CancellationToken ct = default)
    {
        string chatId = Chat.GetChatId(fromId, peerId);
        var peer = await _users.GetUserAsync(peerId, ct) ?? throw new Exception("User not found: " + peerId);
        var getChatResult = await GetOrCreateChat(chatId, fromId, peer, ct);

        var message = await _messages.SaveAsync(fromId, peerId, text, chatId);

        // !isNewChat
        if (!getChatResult.Item2)
            await _chats.UpdateLastMessageAsync(chatId, message, ct);

        message.FromOwner = false;
        await _publisher.PublishAsync(peerId, message.ChatId, UserEventType.NEW_MESSAGE, message, ct);

        await _pushNotifications.SendPushAsync(
            peerId,
            PushNotificationType.CHAT_NEW_ACTION,
            message.ChatId,
            peerId,
            getChatResult.Item1.ChatName!,
            message.Text ?? "[action]",
            peer.ImageUrl, ct);

        return message;
    }

    public async Task<bool> MarkReadUpToAsync(int upToMessageId, int readerId, CancellationToken ct = default)
    {
        var message = await _messages.GetMessageAsync(upToMessageId, ct);
        if (message is null) {
            _logger.LogWarning(LogEvents.ApiUnknownError,
                "MarkReadUpToAsync(): message with fetched upToMessageId (id={upToMessageId}, readerId={readerId}) not found!",
                upToMessageId, readerId
            );
            return false;
        }
        // сообщение не принадлежит юзеру, но юзер является назначением
        // чтобы не дать возможность прочитывать свои сообщения или за других юзеров
        if (!(message.FromId != readerId && message.PeerId == readerId))
        {
            _logger.LogWarning(LogEvents.ApiWeirdRequest,
                "MarkReadUpToAsync(): Attempting to mark someone else's messages as read!" + 
                "(upToMessageId={upToMessageId}; readerId={readerId})",
                upToMessageId, readerId
            );
            return false;
        }

        var readDate = DateTime.UtcNow;
        int updatedCount = await _messages.MarkReadUpToAsync(message.ChatId, message.FromId, message.Index, readDate, ct);
        if (updatedCount == 0) return false;

        var payload = new { chatId = message.ChatId, upToMessageId = message.Id, readDate };
        await _publisher.PublishAsync(message.FromId, message.ChatId, UserEventType.MESSAGE_READED, payload, ct);

        return true;
    }

    public async Task<bool> EditMessage(int ownerId, int messageId, string newText, CancellationToken ct = default)
    {
        var message = await _messages.GetMessageAsync(messageId, ct);
        if (message is null) {
            _logger.LogWarning(LogEvents.ApiUnknownError,
                "EditMessage(): message with fetched messageId (id={messageId}, ownerId={ownerId}) not found!",
                messageId, ownerId
            );
            return false;
        }
        // сообщение должно принадлежать юзеру
        // чтобы не дать возможность редактировать чужие сообщения
        if (message.FromId != ownerId)
        {
            _logger.LogWarning(LogEvents.ApiWeirdRequest,
                "EditMessage(): Attempting to edit someone else's message!" + 
                "(ownerId={ownerId}; messageId={messageId})",
                ownerId, messageId
            );
            return false;
        }

        var editDate = DateTime.UtcNow;
        int updatedCount = await _messages.EditMessage(messageId, newText, editDate, ct);
        if (updatedCount == 0) return false;

        var payload = new { messageId, newText, editDate };
        await _publisher.PublishAsync(message.PeerId, message.ChatId, UserEventType.MESSAGE_EDITED, payload, ct);

        return true;
    }

    public async Task<bool> DeleteMessage(int ownerId, int messageId, CancellationToken ct = default)
    {
        var message = await _messages.GetMessageAsync(messageId, ct);
        if (message is null) {
            _logger.LogWarning(LogEvents.ApiUnknownError,
                "DeleteMessage(): message with fetched messageId (id={messageId}, ownerId={ownerId}) not found!",
                messageId, ownerId
            );
            return false;
        }

        // убрать для возможности удалять чужие сообщения
        if (message.FromId != ownerId)
        {
            _logger.LogWarning(LogEvents.ApiWeirdRequest,
                "DeleteMessage(): Attempting to delete someone else's message!" + 
                "(ownerId={ownerId}; messageId={messageId})",
                ownerId, messageId
            );
            return false;  
        } 

        int deletedCount = await _messages.DeleteMessage(messageId, ct);
        if (deletedCount == 0) return false;

        var payload = new { messageId };
        await _publisher.PublishAsync(message.PeerId, message.ChatId, UserEventType.MESSAGE_DELETED, payload, ct);

        return true;
    }


    #region supportives
    private async Task<(Chat, bool)> GetOrCreateChat(string chatId, int userId, User peer, CancellationToken ct = default)
    {
        var chat = await _chats.GetAsync(chatId, ct);
        bool isNewChat = chat is null;

        if (isNewChat)
        {
            chat = await _chats.CreateAsync(userId, peer.Id, chatId, ct);
            var chatPropertiesForFromId = await _chatProperties.CreateAsync(userId, peer.Id, chatId, ct);
            var chatPropertiesForPeerId = await _chatProperties.CreateAsync(peer.Id, userId, chatId, ct);

            await _userRelations.AddOrCreateRelations(userId, chat.GetMembersForUser(userId), ct);

            chat.ChatProperties = chatPropertiesForFromId;
            await _publisher.PublishAsync(userId, chatId, UserEventType.NEW_CHAT, chat, ct);

            chat.ChatProperties = chatPropertiesForPeerId;
            await _publisher.PublishAsync(peer.Id, chatId, UserEventType.NEW_CHAT, chat, ct);
        } 
        else
        {
            chat!.ChatProperties = await _chatProperties.GetAsync(peer.Id, chatId);
        }

        chat.ChatName ??= peer.Nickname;
        return (chat, isNewChat);
    }
    #endregion
}