namespace PetChat.ApiServer.Model.Configuration
{
    public class AccessConfig
    {
        public string[] ClientKeys { get; private set; } = null!;
        public string[] BannedDevicesList { get; private set; } = null!;
    }
}