using PetChat.ApiServer.Database.Entities.Userspace;

namespace PetChat.ApiServer.Database.Repositories.Interfaces;

public interface IUsersRepository
{
    Task<User> AddUserAsync(string nickname, CancellationToken ct = default);
    Task<User?> GetUserAsync(int userId, CancellationToken ct = default);
    Task<User?> GetUserAsync(string shortName, CancellationToken ct = default);
    Task<User[]> GetUsersAsync(int[] userIds, CancellationToken ct = default);
    Task SetLastSeenNow(int userId, CancellationToken ct = default);
    Task<User[]> SearchUsersAsync(string query, CancellationToken ct = default);
}