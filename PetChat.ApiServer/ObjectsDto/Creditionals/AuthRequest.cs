namespace PetChat.ApiServer.ObjectsDto.Creditionals;

public class AuthRequest
{
    public string Login { get; set; } = default!;
    public string Passwd { get; set; } = default!;
    public bool Refresh { get; set; } = false;
}