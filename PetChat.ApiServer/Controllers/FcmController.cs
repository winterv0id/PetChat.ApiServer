using Microsoft.AspNetCore.Mvc;
using PetChat.ApiServer.Controllers.Base;
using PetChat.ApiServer.Database;
using PetChat.ApiServer.Database.Entities.Serverspace;

namespace PetChat.ApiServer.Controllers;

[Route("")]
public class FcmController(PetChatDbContext database) : IdController
{
    private readonly PetChatDbContext _database = database;

    [HttpGet]
    public async Task<IActionResult> Register(string installationId, CancellationToken ct)
    {
        int userId = GetUserId();

        var registration = await _database.UserFcmRegistrations
            .FindAsync([userId, installationId], ct);

        if (registration is null)
        {
            _database.UserFcmRegistrations.Add(new UserFcmRegistration
            {
                UserId = userId,
                InstallationId = installationId,
                RegisteredAt = DateTime.UtcNow
            });
            await _database.SaveChangesAsync(ct);
        }

        return Accepted();
    }

    [HttpGet]
    public async Task<IActionResult> Unregister(string installationId, CancellationToken ct)
    {
        int userId = GetUserId();

        var registration = await _database.UserFcmRegistrations
            .FindAsync([userId, installationId], ct);

        if (registration is not null)
        {
            _database.UserFcmRegistrations.Remove(registration);
            await _database.SaveChangesAsync(ct);
        }

        return Accepted();
    }
}