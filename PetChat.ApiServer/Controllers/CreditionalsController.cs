using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using PetChat.ApiServer.Database.Repositories.Interfaces;
using PetChat.ApiServer.Model;
using PetChat.ApiServer.ObjectsDto.Creditionals;
using PetChat.ApiServer.Utils;

namespace PetChat.ApiServer.Controllers;

[ApiController]
[Route("")]
public class CreditionalsController(IUserCreditionalsRepository creditionalsRepository, IUserEventRepository userEvents,
    IUsersRepository usersRepository, IConfiguration appConfig, ILogger<CreditionalsController> logger) : ControllerBase
{
    private readonly IUserCreditionalsRepository _creditionalsRepository = creditionalsRepository;
    private readonly IUsersRepository _usersRepository = usersRepository;
    private readonly IUserEventRepository _userEvents = userEvents;
    private readonly ILogger<CreditionalsController> _logger = logger; 
    private readonly byte[] key = Encoding.UTF8.GetBytes(appConfig["JwtSymmetricKey"]!);

    [HttpPost]
    public async Task<IActionResult> Auth([FromBody] AuthRequest authRequest, CancellationToken ct)
    {
        if (!await _creditionalsRepository.IsRegistrationExists(authRequest.Login))
            return BadRequest("is not registered.");

        StringValues clientKey = Request.Headers["X-CLIENT-KEY"],
            deviceId = Request.Headers["X-DEVICE-ID"];

        int userId = await _creditionalsRepository.GetUserId(authRequest.Login, ct);

        _logger.LogInformation(LogEvents.Authorization, "Auth for userId={userId}, " + 
            "device-id={deviceId}, client-key={clientKey}, refresh={refresh} requested. ", 
            userId, deviceId, clientKey, authRequest.Refresh
        );

        string loginPasswdHash = await _creditionalsRepository.GetPasswordHash(authRequest.Login, ct);

        if (!Pbkdf2Hasher.VerifyPassword(authRequest.Passwd, loginPasswdHash))
        {
            _logger.LogWarning(LogEvents.AuthorizationUnsuccessful, 
                "UserId={userId}, device-id={deviceId}, client-key={clientKey} not authorized - wrong password.",
                userId, deviceId, clientKey
            );
            return Unauthorized("no access."); 
        }

        _logger.LogInformation(LogEvents.AuthorizationSuccessful,
            "Getting JWT-Token (access) for userId={userId}, device-id={deviceId}, client-key={clientKey}...",
            userId, deviceId, clientKey
        );

        string token = GenerateJwtToken(userId, authRequest.Login, deviceId!, clientKey!);
        var cUser = await _usersRepository.GetUserAsync(userId, ct);

        if (!authRequest.Refresh) 
            await _userEvents.DeteleUserEvents(userId, ct);

        return Ok(new { user = cUser, accessToken = token });
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterRequest registerRequest, CancellationToken ct)
    {
        if (await _creditionalsRepository.IsRegistrationExists(registerRequest.Login, ct))
            return BadRequest("user already registered.");

        if (!PasswordValidator.IsSafe(registerRequest.Passwd))
            return BadRequest("password does not meet security requirements.");

        StringValues clientKey = Request.Headers["X-CLIENT-KEY"],
            deviceId = Request.Headers["X-DEVICE-ID"];

        _logger.LogInformation(LogEvents.Registration, "Registration for ip={ip} requested. " +
            "(X-CLIENT-KEY={X-CLIENT-KEY}, X-DEVICE-ID={X-DEVICE-ID})", 
            HttpContext.Connection.RemoteIpAddress, clientKey!, deviceId!
        );

        string passwdHash = Pbkdf2Hasher.HashPassword(registerRequest.Passwd);
        var user = await _usersRepository.AddUserAsync(registerRequest.Nickname);

        await _creditionalsRepository.AddCreditionalsAsync(
            registerRequest.Login, passwdHash, user.Id, deviceId!, ct);

        string token = GenerateJwtToken(user.Id, registerRequest.Login, deviceId!, clientKey!);
        return Ok(new { user, accessToken = token });
    }

    private string GenerateJwtToken(int userId, string login, string deviceId, string clientKey)
    {
        List<Claim> claims = [
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, login)
        ];

        JwtSecurityToken jwt = new(
            issuer: "PetChatApi",
            audience: "PetChatClient",
            claims: claims,
            expires: DateTime.UtcNow.Add(TimeSpan.FromDays(7)),
            signingCredentials: new SigningCredentials
            (
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256
            )
        );

        var encodedJwt = new JwtSecurityTokenHandler().WriteToken(jwt);

        _logger.LogInformation(LogEvents.AuthorizationSuccessful,
            "JWT-Token (access) for user uid={userId}, device-id={deviceId}, client-key={clientKey}: " + 
            "valid_to: {ValidTo}, audiences: {Audiences}.",
            userId, deviceId, clientKey, jwt.ValidTo.ToString("dd/MM/yyyy HH:mm:ss"),
            string.Join(", ", jwt.Audiences)
        );

        return encodedJwt;
    }
}