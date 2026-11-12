using NVGI.Projet.Importtool.Services;
using Microsoft.AspNetCore.Mvc;

namespace NVGI.Projet.Importtool.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly ApiKeyStore _keys;

    public AuthController(AuthService auth, ApiKeyStore keys)
    {
        _auth = auth;
        _keys = keys;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest? request, CancellationToken cancellationToken)
    {
        var (ok, error) = await _auth.ValidateAsync(request?.Login, request?.Password, cancellationToken);
        if (ok)
            return Ok(new { apiKey = _keys.Issue() });

        if (error is "Enter a login and password." or "Login or password is incorrect.")
            return Unauthorized(new { message = error });

        return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = error });
    }

    [HttpGet("session")]
    public IActionResult Session() => Ok(new { ok = true });

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        _keys.Revoke(ApiKeyMiddleware.ReadKey(Request));
        return Ok(new { ok = true });
    }

    public sealed class LoginRequest
    {
        public string? Login { get; set; }
        public string? Password { get; set; }
    }
}
