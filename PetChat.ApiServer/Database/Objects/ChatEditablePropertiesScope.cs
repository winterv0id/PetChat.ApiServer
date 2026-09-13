namespace PetChat.ApiServer.Database.Objects;

public class ChatEditablePropertiesScope
{
    public bool? IsNotificationsEnabled { get; set; }
    public bool? IsArchived { get; set; }
    public bool? IsPinned { get; set; }
}