namespace PetChat.ApiServer.Database.Entities.Userspace;

// свойства чата, применяемые к конкретному пользователю
public class ChatProperties
{
    public int Id { get; set; }
    public string ChatId { get; set; } = default!;
    public int UserId { get; set; } // к какому юзеру относится
    public int PeerId { get; set; }
    public User? Peer { get; set; } // ignore
    public bool NotificationsEnabled { get; set; } = true;
    public bool Archived { get; set; } = false;
    public bool Pinned { get; set; } = false;
}