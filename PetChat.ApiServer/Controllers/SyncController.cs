using Microsoft.AspNetCore.Mvc;
using PetChat.ApiServer.Controllers.Base;
using PetChat.ApiServer.Database.Repositories.Interfaces;

namespace PetChat.ApiServer.Controllers;

[Route("")]
public class SyncController(IUserEventRepository events) : IdController
{
    private readonly IUserEventRepository _events = events;

    private const int SYNC_MAX_LIMIT_VALUE = 2000;

    [HttpGet]
    public async Task<IActionResult> Sync(long after, int? limit, CancellationToken ct)
    {
        limit ??= 200;
        if (limit > SYNC_MAX_LIMIT_VALUE) 
            return BadRequest($"limit max value is {SYNC_MAX_LIMIT_VALUE}. fetched: {limit}.");
        
        int userId = GetUserId();
        var evts = await _events.GetEventsSinceAsync(userId, after, (int)limit, ct);
        return Ok(new { events = evts, hasMore = evts.Count == limit });
    }
}