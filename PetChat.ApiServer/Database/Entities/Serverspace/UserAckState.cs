namespace PetChat.ApiServer.Database.Entities.Serverspace;

// подтверждённая клиентом позиция - клиент сохранил событие локально 
public class UserAckState
{
    public int UserId { get; set; } = default!; // pkey
    public long AcknowledgedSequence { get; set; }
    public DateTime AcknowledgedAt { get; set; }
}