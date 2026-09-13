using PetChat.ApiServer.Database.Entities.Userspace;

namespace PetChat.ApiServer.Services.Synchronization.Model;

public class UserSnapshot(Chat[] chats, Message[] messages)
{
    public Chat[] Chats { get; init; } = chats;
    public Message[] Messages { get; init; } = messages;
}