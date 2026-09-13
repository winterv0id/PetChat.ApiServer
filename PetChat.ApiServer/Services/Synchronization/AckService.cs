using PetChat.ApiServer.Database.Repositories.Interfaces;
using PetChat.ApiServer.Services.Synchronization.Interfaces;

namespace PetChat.ApiServer.Services.Synchronization;

public class AckService(IUserAckRepository repository) : IAckService
{
    private readonly IUserAckRepository _repository = repository;

    public Task WriteAckAsync(int userId, long sequence, CancellationToken ct = default)
    {
        return _repository.WriteIfGreaterAsync(userId, sequence, ct);
    }
}