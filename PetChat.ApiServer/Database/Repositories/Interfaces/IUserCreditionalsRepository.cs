namespace PetChat.ApiServer.Database.Repositories.Interfaces;

public interface IUserCreditionalsRepository
{
    Task AddCreditionalsAsync(string login, string passwdHash, int userId, string deviceId, CancellationToken ct = default);
    Task<string> GetPasswordHash(string login, CancellationToken ct = default);
    Task<int> GetUserId(string login, CancellationToken ct = default);
    Task<bool> IsRegistrationExists(string login, CancellationToken ct = default);
}