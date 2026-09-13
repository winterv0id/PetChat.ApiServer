using Microsoft.AspNetCore.Mvc;
using PetChat.ApiServer.Controllers.Base;
using PetChat.ApiServer.Database.Objects;
using PetChat.ApiServer.Database.Repositories.Interfaces;
using PetChat.ApiServer.Model;

namespace PetChat.ApiServer.Controllers;

[Route("")]
public class ChatsController(IChatsRepository chats, IChatPropertiesRepository chatProperties, 
    IMessagesRepository messages, ILogger<ChatsController> logger) : IdController
{
    private readonly IChatsRepository _chats = chats;
    private readonly IChatPropertiesRepository _chatProperties = chatProperties;
    private readonly IMessagesRepository _messages = messages;
    private readonly ILogger<ChatsController> _logger = logger;

    private const int GETHISTORY_MAX_LIMIT_VALUE = 200;


    [HttpGet]
    public async Task<IActionResult> UpdateProperties([FromQuery] string chatId, 
        [FromQuery] ChatEditablePropertiesScope? scope, CancellationToken ct)
    {
        if (scope is null)
            return BadRequest("Request no contains chat properties.");

        int userId = GetUserId();

        int updatedCount = await _chatProperties.UpdateProperties(userId, chatId, scope, ct);
        if (updatedCount is 0)
            return BadRequest("Nothing to update.");

        return Accepted();
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory(string chatId, int before, int? limit, CancellationToken ct)
    {
        limit ??= 200;
        if (limit > 200)
             return BadRequest($"limit max value is {GETHISTORY_MAX_LIMIT_VALUE}. fetched: {limit}.");

        int userId = GetUserId();
        if (!await _chats.UserIsMember(userId, chatId))
        {
            _logger.LogWarning(LogEvents.ApiWeirdRequest,
                "MarkReadUpToAsync(): Attempting to get history from someone else's chat!" + 
                "(chatId={cahtId}; userId={userId})", chatId, userId
            );
            return BadRequest("You are not a member.");
        }

        var mWindow = await _messages.GetMessagesBeforeAsync(chatId, before, (int)limit, ct);
        
        foreach (var message in mWindow)
            if (message.FromId == userId) message.FromOwner = true;

        return Ok(new { 
            messages = mWindow, 
            length = mWindow.Length, 
            hasMore = mWindow.Length == limit 
        });
    }

    [HttpGet]
    public async Task<IActionResult> Delete()
    {
        //TODO
        return NoContent();
    }
}