namespace PetChat.ApiServer.Database.Entities.Serverspace;

public class UserCreditionals
{
    public int Id { get; set; }
    public string Login { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string DeviceId { get; set; } = default!;
    public int UserId { get; set; }
}