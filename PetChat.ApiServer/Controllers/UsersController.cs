using Microsoft.AspNetCore.Mvc;
using PetChat.ApiServer.Controllers.Base;
using PetChat.ApiServer.Database.Repositories.Interfaces;
using PetChat.ApiServer.SignalR.PresenceTracker;

namespace PetChat.ApiServer.Controllers;

[Route("")]
public class UsersController(IUsersRepository users, IPresenceTracker presenceTracker) : IdController
{
    private readonly IUsersRepository _users = users;
    private readonly IPresenceTracker _presenceTracker = presenceTracker;

    private const int SEARCH_MIN_QUERY_LENGTH = 5;

    [HttpGet]
    public async Task<IActionResult> Search(string query, CancellationToken ct)
    {
        if (query.Length < SEARCH_MIN_QUERY_LENGTH)
            return BadRequest("query too short - min length is " + SEARCH_MIN_QUERY_LENGTH);

        var users = await _users.SearchUsersAsync(query, ct);

        foreach (var user in users)
            user.IsOnline = _presenceTracker.IsOnline(user.Id);

        return Ok(new { users });
    }

    [HttpGet]
    public async Task<IActionResult> Get(int? userId, string? shortName, CancellationToken ct)
    {
        if (userId is null && shortName is null) {
            return BadRequest("none of the identifier types were passed.");
        }

        Database.Entities.Userspace.User? user;

        if (userId is not null)
            user = await _users.GetUserAsync((int)userId, ct);
        else 
            user = await _users.GetUserAsync(shortName!, ct);

        if (user is null)
            return BadRequest("user not found.");

        user.IsOnline = _presenceTracker.IsOnline(user.Id);

        return Ok(new { user });
    }
}