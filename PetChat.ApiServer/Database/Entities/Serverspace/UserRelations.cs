namespace PetChat.ApiServer.Database.Entities.Serverspace;

//помогает IPresenceNotificationService отправлять юзеру статус всех пользователей, с которыми он общается
public class UserRelations
{
    public UserRelations() {}
    public UserRelations(int userId, int[] relations)
    {
        UserId = userId;
        RelationsUsers = [..relations];
    }
    public int UserId { get; set; } = default!; // pkey
    public HashSet<int> RelationsUsers { get; set; } = [];
}