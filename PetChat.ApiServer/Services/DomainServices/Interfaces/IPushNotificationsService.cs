public interface IPushNotificationsService
{
    Task SendPushAsync(int userId, string notificationType, string? chatId, int peerId,
        string title, string preview, string? avatarUrl, CancellationToken ct = default);

    //отправка на конкретную инсталляцию
    Task SendToInstallationAsync(string installationId, string title, string body, Dictionary<string, string> data, CancellationToken ct = default);
}