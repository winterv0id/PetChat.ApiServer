namespace PetChat.ApiServer.Database.Entities.Serverspace;

public class UserEvent
{
    public UserEvent() {}

    public UserEvent(int userId, string? chatId, string eventType, string payload)
    {
        Sequence = -1;
        UserId = userId;
        ChatId = chatId;
        EventType = eventType;
        PayloadJson = payload;
    }

    public long Sequence { get; set; } // 1 событие = +1 seq
    public int UserId { get; set; }
    public string? ChatId { get; set; }
    public string EventType { get; set; } = default!;
    public string PayloadJson { get; set; } = default!; // jsonb
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}