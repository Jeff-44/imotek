using Imotek.API.Configuration;
using Imotek.Application.Auth;
using Imotek.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Imotek.API.Controllers;

[Route("api/auth")]
public sealed class AuthController(IAuthService auth, ICurrentUser currentUser) : ApiControllerBase
{
    /// <summary>Creates an Agent or Client account.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiSecurity.AuthRateLimit)]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.RegisterAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Me), result.Value)
            : ToProblem(result.Error!);
    }

    /// <summary>Returns a bearer token: { tokenType, accessToken, expiresIn, refreshToken }.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiSecurity.AuthRateLimit)]
    public async Task<ActionResult> Login(LoginRequest request)
    {
        var result = await auth.CreateLoginPrincipalAsync(request);
        return result.IsSuccess
            ? SignIn(result.Value, IdentityConstants.BearerScheme)
            : ToProblem(result.Error!);
    }

    /// <summary>Exchanges a refresh token for a new access token and refresh token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiSecurity.AuthRateLimit)]
    public async Task<ActionResult> Refresh(RefreshRequest request)
    {
        var result = await auth.CreateRefreshPrincipalAsync(request);
        return result.IsSuccess
            ? SignIn(result.Value, IdentityConstants.BearerScheme)
            : ToProblem(result.Error!);
    }

    /// <summary>
    /// Revokes every refresh token of the user, on all devices. Access tokens already
    /// issued stay valid until they expire (Auth:AccessTokenMinutes).
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult> Logout()
    {
        var result = currentUser.UserId is { } id ? await auth.RevokeSessionsAsync(id) : Error.Unauthorized();
        return NoContentOrProblem(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> Me()
    {
        var user = currentUser.UserId is { } id ? await auth.GetUserAsync(id) : null;
        return user is null ? ToProblem(Error.Unauthorized()) : Ok(user);
    }

    /// <summary>Updates the name and phone number (E.164-normalized). Agents must keep a phone number.</summary>
    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> UpdateMe(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        currentUser.UserId is { } id
            ? OkOrProblem(await auth.UpdateProfileAsync(id, request, cancellationToken))
            : ToProblem(Error.Unauthorized());

    /// <summary>Asks to become an agent. The Agent role is only granted when an admin approves.</summary>
    [HttpPost("agent-request")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> RequestAgent(AgentRequestBody request, CancellationToken cancellationToken) =>
        currentUser.UserId is { } id
            ? OkOrProblem(await auth.RequestAgentAsync(id, request, cancellationToken))
            : ToProblem(Error.Unauthorized());
}
