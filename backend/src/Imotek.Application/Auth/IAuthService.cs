using System.Security.Claims;
using Imotek.Application.Common;

namespace Imotek.Application.Auth;

public sealed record RegisterRequest(string? Email, string? Password, string? FullName, string? Role);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record RefreshRequest(string? RefreshToken);

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

    /// <summary>
    /// Exchanges a refresh token for a fresh principal. Fails if the token is expired,
    /// tampered with, or was issued before the user's sessions were revoked.
    /// </summary>
    Task<Result<ClaimsPrincipal>> CreateRefreshPrincipalAsync(RefreshRequest request);

    /// <summary>Invalidates every refresh token of the user (all devices).</summary>
    Task<Result> RevokeSessionsAsync(Guid userId);

    Task<UserResponse?> GetUserAsync(Guid userId);
}
