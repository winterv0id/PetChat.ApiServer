namespace PetChat.ApiServer.Database.Entities.Userspace;

public class User
{
    public int Id { get; set; }
    public string? ShortName { get; set; } 
    public string Nickname { get; set; } = default!;
    public string? Status { get; set; }
    public bool IsOnline { get; set; } = false;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public string? ImageUrl { get; set; } = "https://tinyurl.com/4ehw4jak"; //test
}