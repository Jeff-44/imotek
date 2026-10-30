using Imotek.Application.Auth;
using Imotek.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Imotek.API.Controllers;

[Route("api/auth")]
public sealed class AuthController(IAuthService auth, ICurrentUser currentUser) : ApiControllerBase
{
    /// <summary>Creates an Agent or Client account.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
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
    public async Task<ActionResult> Login(LoginRequest request)
    {
        var result = await auth.CreateLoginPrincipalAsync(request);
        return result.IsSuccess
            ? SignIn(result.Value, IdentityConstants.BearerScheme)
            : ToProblem(result.Error!);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> Me()
    {
        var user = currentUser.UserId is { } id ? await auth.GetUserAsync(id) : null;
        return user is null ? ToProblem(Error.Unauthorized()) : Ok(user);
    }
}
