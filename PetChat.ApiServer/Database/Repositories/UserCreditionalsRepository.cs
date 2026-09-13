using Microsoft.EntityFrameworkCore;
using PetChat.ApiServer.Database.Entities.Serverspace;
using PetChat.ApiServer.Database.Repositories.Interfaces;

namespace PetChat.ApiServer.Database.Repositories;

public class UserCreditionalsRepository(PetChatDbContext petChatDb) : IUserCreditionalsRepository
{
    private readonly PetChatDbContext _petChatDb = petChatDb;
    
    public async Task AddCreditionalsAsync(string login, string passwdHash, int userId, string deviceId, CancellationToken ct = default)
    {
        var userCreditionals = new UserCreditionals()
        {
            Login = login,
            PasswordHash = passwdHash,
            DeviceId = deviceId,
            UserId = userId
        };
        await _petChatDb.UserCreditionals.AddAsync(userCreditionals, ct);
        await _petChatDb.SaveChangesAsync(ct);
    }

    public async Task<int> GetUserId(string login, CancellationToken ct = default)
    {
        return await _petChatDb.UserCreditionals
            .Where(uc => uc.Login == login)
            .Select(uc => uc.UserId)
            .FirstAsync(ct);
    }

    public async Task<string> GetPasswordHash(string login, CancellationToken ct = default)
    {
        return await _petChatDb.UserCreditionals
            .Where(uc => uc.Login == login)
            .Select(uc => uc.PasswordHash)
            .FirstAsync(ct);
    }

    public async Task<bool> IsRegistrationExists(string login, CancellationToken ct = default)
    {
        return await _petChatDb.UserCreditionals
            .AnyAsync(uc => uc.Login == login, ct);
    }
}