using PetChat.ApiServer.Database.Repositories.Interfaces;

namespace PetChat.ApiServer.Services.BackgroundServices;

public class UserEventTrimJob(IServiceScopeFactory scopeFactory) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IUserEventRepository>();

            await repo.TrimAcknowledgedOrOldEventsAsync(
                ackMargin: 50,//запас
                maxLifeSpan: TimeSpan.FromDays(7));

            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }
}