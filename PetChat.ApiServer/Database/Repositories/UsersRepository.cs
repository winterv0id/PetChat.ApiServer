using Microsoft.EntityFrameworkCore;
using PetChat.ApiServer.Database.Entities.Userspace;
using PetChat.ApiServer.Database.Repositories.Interfaces;

namespace PetChat.ApiServer.Database.Repositories;

public class UsersRepository(PetChatDbContext petChatDb) : IUsersRepository
{
    private readonly PetChatDbContext _petChatDb = petChatDb;

    public async Task<User> AddUserAsync(string nickname, CancellationToken ct = default)
    {
        var user = new User()
        {
            Nickname = nickname
        };
        await _petChatDb.Users.AddAsync(user, ct);
        await _petChatDb.SaveChangesAsync(ct);

        return user;
    }

    public async Task<User?> GetUserAsync(int userId, CancellationToken ct = default)
    {
        return await _petChatDb.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
    }

    public async Task<User?> GetUserAsync(string shortName, CancellationToken ct = default)
    {
        return await _petChatDb.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.ShortName == shortName, ct);
    }

    public async Task<User[]> GetUsersAsync(int[] userIds, CancellationToken ct = default)
    {
        return await _petChatDb.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToArrayAsync(ct);
    }

    public async Task<User[]> SearchUsersAsync(string query, CancellationToken ct = default)
    {
        return await _petChatDb.Users
            .AsNoTracking()
            //Nickname or ShortName
            .Where(u => u.Nickname.StartsWith(query) || (u.ShortName != null && u.ShortName.StartsWith(query)))
            .ToArrayAsync(ct);
    }

    public async Task SetLastSeenNow(int userId, CancellationToken ct = default)
    {
        var user = await _petChatDb.Users.FirstAsync(u => u.Id == userId, ct);
        user.LastSeen = DateTime.UtcNow;
        await _petChatDb.SaveChangesAsync(ct);
    }
}