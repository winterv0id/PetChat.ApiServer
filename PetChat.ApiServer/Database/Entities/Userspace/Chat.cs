namespace PetChat.ApiServer.Database.Entities.Userspace;

public class Chat
{
    public string Id { get; set; } = default!;
    public int MessageIndex { get; set; } = 0;
    public List<int> Members { get; set; } = [];
    public Message? LastMessage { get; set; } //ignore
    public ChatProperties? ChatProperties { get; set; } //ignore
    public string? ImageUrl { get; set; }
    public string? ChatName { get; set; }
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    public static string GetChatId(int userId, int peerId)
    {
        int min = Math.Min(userId, peerId);
        int max = Math.Max(userId, peerId);
        return $"{min}_{max}";
    }

    public int[] GetMembersForUser(int userId)
    {
        return [.. Members.Where(m => m != userId)];
    }
}