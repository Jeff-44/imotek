using System.Security.Claims;
using Imotek.Application.Common;

namespace Imotek.Application.Auth;

public sealed record RegisterRequest(string? Email, string? Password, string? FullName, string? Role);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record UserResponse(Guid Id, string Email, string FullName, IReadOnlyList<string> Roles);

public interface IAuthService
{
    /// <summary>Creates an Agent or Client account. Admin accounts cannot be self-registered.</summary>
    Task<Result<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the credentials (with lockout) and returns the principal to sign in.
    /// Unknown email and wrong password produce the same error.
    /// </summary>
    Task<Result<ClaimsPrincipal>> CreateLoginPrincipalAsync(LoginRequest request);

    Task<UserResponse?> GetUserAsync(Guid userId);
}
