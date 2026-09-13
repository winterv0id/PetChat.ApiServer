namespace PetChat.ApiServer.Database.Repositories.Interfaces;

public interface IUserAckRepository
{
    Task WriteIfGreaterAsync(int userId, long sequence, CancellationToken ct = default);
}