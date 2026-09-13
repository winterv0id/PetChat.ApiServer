namespace PetChat.ApiServer.Database.Entities.Serverspace;

public class UserFcmRegistration
{
    public int UserId { get; set; }
    public string InstallationId { get; set; } = default!;
    public DateTime RegisteredAt { get; set; }
}