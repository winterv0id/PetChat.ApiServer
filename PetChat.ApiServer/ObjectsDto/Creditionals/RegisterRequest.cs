namespace PetChat.ApiServer.ObjectsDto.Creditionals;

public class RegisterRequest
{
    public string Login { get; set; } = default!;
    public string Passwd { get; set; } = default!;
    public string Nickname { get; set; } = default!;
}