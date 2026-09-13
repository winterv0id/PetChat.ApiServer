namespace PetChat.ApiServer.Database.Entities.Userspace;

public class Message
{
    public int Id { get; set; }
    public int FromId { get; set; }
    public int PeerId { get; set; }
    public int Index { get; set; }
    public string ChatId { get; set; } = default!;
    public string? Text { get; set; }
    public bool FromOwner { get; set; } //ignore
    public bool Forwarded { get; set; } = false;
    public bool Readed { get; set; } = false;
    public bool Edited { get; set; } = false;
    public bool Deleted { get; set; } = false;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public DateTime? EditDate { get; set; }
    public DateTime? ReadDate { get; set; }
}