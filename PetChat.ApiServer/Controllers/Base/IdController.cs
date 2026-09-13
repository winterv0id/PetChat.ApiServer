using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PetChat.ApiServer.Controllers.Base;

[ApiController]
[Authorize]
public abstract class IdController : ControllerBase
{
    protected int GetUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}