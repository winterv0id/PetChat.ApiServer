namespace PetChat.ApiServer.Database.Repositories.Interfaces;

public interface IUserRelationsRepository
{
    Task<int[]?> GetUserRelations(int userId, CancellationToken ct = default);
    Task AddOrCreateRelations(int userId, int[] relations, CancellationToken ct = default);
    Task RemoveRelations(int userId, int[] relations, CancellationToken ct = default);
}