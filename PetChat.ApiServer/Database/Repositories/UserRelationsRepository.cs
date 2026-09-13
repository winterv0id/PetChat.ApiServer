using Microsoft.EntityFrameworkCore;
using PetChat.ApiServer.Database.Entities.Serverspace;
using PetChat.ApiServer.Database.Repositories.Interfaces;

namespace PetChat.ApiServer.Database.Repositories;

public class UserRelationsRepository(PetChatDbContext petChatDb) : IUserRelationsRepository
{
    private readonly PetChatDbContext _petChatDb = petChatDb;

    public async Task AddOrCreateRelations(int userId, int[] relations, CancellationToken ct = default)
    {
        var relationsEntry = await GetUserRelationsEntry(userId, ct: ct);

        if (relationsEntry is null)
        {
            relationsEntry = new UserRelations(userId, relations);
            await _petChatDb.UserRelations.AddAsync(relationsEntry, ct);
        } 
        else
        {
            relationsEntry.RelationsUsers.UnionWith(relations);
        }
        await _petChatDb.SaveChangesAsync(ct);    
    }

    public async Task<int[]?> GetUserRelations(int userId, CancellationToken ct = default)
    {
        var relationsEntry = await GetUserRelationsEntry(userId, ct: ct);
        return relationsEntry?.RelationsUsers?.ToArray();
    }

    public async Task RemoveRelations(int userId, int[] relations, CancellationToken ct = default)
    {
        var relationsEntry = await GetUserRelationsEntry(userId, tracking: false, ct: ct);
        if (relationsEntry == null) return;

        foreach (int value in relations)
            relationsEntry.RelationsUsers.Remove(value);

        await _petChatDb.SaveChangesAsync(ct);
    }

    private async Task<UserRelations?> GetUserRelationsEntry(int userId, bool tracking = true, CancellationToken ct = default)
    {
        if (tracking)
            return await _petChatDb.UserRelations.FindAsync(userId, ct);
        else
        {
            return await _petChatDb.UserRelations
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);
        }
    }
}