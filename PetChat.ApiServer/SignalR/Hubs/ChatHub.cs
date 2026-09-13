using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PetChat.ApiServer.Database.Repositories.Interfaces;
using PetChat.ApiServer.ObjectsDto.Messages;
using PetChat.ApiServer.Services.DomainServices.Interfaces;
using PetChat.ApiServer.Services.Synchronization.Interfaces;
using PetChat.ApiServer.Services.Synchronization.Model;
using PetChat.ApiServer.SignalR.PresenceTracker;

namespace PetChat.ApiServer.SignalR.Hubs;

[Authorize]
public class ChatHub(IMessagesService messages, IPresenceEventService presenceNotifications,
    ISyncService sync, IAckService ack, IUserEventPublisher eventPublisher, 
    IPresenceTracker presence, IUsersRepository users) : Hub
{
    private readonly IMessagesService _messages = messages;
    private readonly ISyncService _sync = sync;
    private readonly IAckService _ack = ack;
    private readonly IPresenceEventService _presenceNotifications = presenceNotifications;
    private readonly IPresenceTracker _presence = presence;
    private readonly IUserEventPublisher _eventPublisher = eventPublisher;
    private readonly IUsersRepository _users = users;


    private int UserId => int.Parse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new HubException("Не удалось определить пользователя"));

    #region Connection
    public override async Task OnConnectedAsync()
    {
        int userId = UserId;

        await _presence.UserConnected(userId, Context.ConnectionId);
        await _presenceNotifications.SendOnlineAsync(userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        int userId = UserId;
        
        await _users.SetLastSeenNow(userId);
        await _presence.UserDisconnected(userId, Context.ConnectionId);
        await _presenceNotifications.SendOfflineAsync(userId);
        await base.OnDisconnectedAsync(ex);
    }
    #endregion

    #region Actions
    public async Task UserTyping(int peerId)
    {
        var payload = new { userId = UserId };
        await _eventPublisher.PublishWithoutSavingAsync(
            peerId, null, UserEventType.USER_TYPING, payload
        );
    }

    public async Task UserStoppedTyping(int peerId)
    {
        var payload = new { userId = UserId };
        await _eventPublisher.PublishWithoutSavingAsync(
            peerId, null, UserEventType.USER_STOPPED_TYPING, payload
        );
    }
    #endregion

    #region Sync
    public Task<SyncResult> Sync(long? lastSequence) => _sync.SyncAsync(UserId, lastSequence);

    public Task Ack(long sequence) => _ack.WriteAckAsync(UserId, sequence);
    #endregion
    
    #region Requests
    public async Task<SendMessageResult> SendMessage(int peerId, string? text)
    {
        var message = await _messages.SendAsync(UserId, peerId, text);
        return new SendMessageResult(message.Id, message.Index, message.Date, message.ChatId);
    }

    public async Task<bool> MarkMessagesReadUpTo(int upToMessageId)
    {
        return await _messages.MarkReadUpToAsync(upToMessageId, UserId);
    }

    public async Task<bool> EditMessage(int messageId, string newText)
    {
        return await _messages.EditMessage(UserId, messageId, newText);
    }

    public async Task<bool> DeleteMessage(int messageId)
    {
        return await _messages.DeleteMessage(UserId, messageId);
    }
    #endregion
}