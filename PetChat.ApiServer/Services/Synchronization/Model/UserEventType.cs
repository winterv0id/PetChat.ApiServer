namespace PetChat.ApiServer.Services.Synchronization.Model;

public static class UserEventType
{
    public const string NEW_MESSAGE = "new_message";
    public const string MESSAGE_EDITED = "message_edited";
    public const string MESSAGE_DELETED = "message_deleted";
    public const string MESSAGE_READED = "message_readed";
    public const string NEW_CHAT = "new_chat";

    public const string USER_OFFLINE = "user_offline";
    public const string USER_ONLINE = "user_online";

    public const string USER_TYPING = "user_typing";
    public const string USER_STOPPED_TYPING = "user_stopped_typing";
}