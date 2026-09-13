using FirebaseAdmin.Messaging;
using Microsoft.EntityFrameworkCore;
using PetChat.ApiServer.Database;
using PetChat.ApiServer.SignalR.PresenceTracker;

namespace PetChat.ApiServer.Services.DomainServices;

public class FcmPushNotificationsService(PetChatDbContext database, IPresenceTracker presence) : IPushNotificationsService
{
    private readonly PetChatDbContext _database = database;
    private readonly IPresenceTracker _presence = presence; 

    public async Task SendPushAsync(int userId, string notificationType, string? chatId, int peerId,
        string title, string preview, string? avatarUrl, CancellationToken ct = default)
    {
        if (_presence.IsOnline(userId)) return;
        
        var installationIds = await _database.UserFcmRegistrations
            .Where(r => r.UserId == userId)
            .Select(r => r.InstallationId)
            .ToListAsync(ct);

        if (installationIds.Count is 0) return;

        var message = new MulticastMessage
        {
            Fids = installationIds,
            Data = new Dictionary<string, string?>
            {
                ["nType"] = notificationType,
                ["chatId"] = chatId,
                ["peerId"] = peerId.ToString(),
                ["title"] = title,
                ["preview"] = preview,
                ["avatarUrl"] = avatarUrl
            }
        };

        var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message, ct);
        await RemoveDeadRegistrationsAsync(userId, installationIds, response, ct);
    }

    public async Task SendToInstallationAsync(string installationId, string title, string body, 
        Dictionary<string, string> data, CancellationToken ct = default)
    {
        var message = new Message
        {
            Fid = installationId,
            Data = data
        };

        try
        {
            await FirebaseMessaging.DefaultInstance.SendAsync(message, ct);
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode == MessagingErrorCode.Unregistered)
        {
            await RemoveRegistrationByInstallationIdAsync(installationId, ct);
        }
    }

    private async Task RemoveDeadRegistrationsAsync(int userId, List<string> installationIds, BatchResponse response, CancellationToken ct)
    {
        for (int i = 0; i < response.Responses.Count; i++)
        {
            var result = response.Responses[i];
            if (!result.IsSuccess && result.Exception?.MessagingErrorCode == MessagingErrorCode.Unregistered)
            {
                var deadId = installationIds[i];
                var entity = await _database.UserFcmRegistrations.FindAsync([userId, deadId], ct);
                if (entity != null) _database.UserFcmRegistrations.Remove(entity);
            }
        }
        await _database.SaveChangesAsync(ct);
    }

    private async Task RemoveRegistrationByInstallationIdAsync(string installationId, CancellationToken ct)
    {
        var entities = await _database.UserFcmRegistrations
            .Where(r => r.InstallationId == installationId)
            .ToListAsync(ct);

        _database.UserFcmRegistrations.RemoveRange(entities);
        await _database.SaveChangesAsync(ct);
    }
}