using System.Security.Claims;
using Imotek.Application.Common;

namespace Imotek.Application.Auth;

/// <summary>
/// Every account starts as a Client. <see cref="RequestAgent"/> files an agent request (phone and
/// public-display consent required); it never grants the Agent role by itself.
/// <see cref="Role"/> is kept for older clients: "Client" is accepted, "Agent" means
/// <c>RequestAgent = true</c>, anything else is rejected.
/// </summary>
public sealed record RegisterRequest(
    string? Email,
    string? Password,
    string? FullName,
    string? Role = null,
    bool RequestAgent = false,
    string? PhoneNumber = null,
    bool PhonePublicConsent = false,
    string? AgentRequestMessage = null);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record RefreshRequest(string? RefreshToken);

/// <summary>Profile fields the user edits. A phone stays mandatory once an agent request exists.</summary>
public sealed record UpdateProfileRequest(string? FullName, string? PhoneNumber, bool PhonePublicConsent);

public sealed record AgentRequestBody(string? PhoneNumber, bool PhonePublicConsent, string? Message);

public sealed record RejectAgentRequestBody(string? Reason);

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    string? PhoneNumber,
    bool PhonePublicConsent,
    AgentStatus AgentStatus,
    DateTime? AgentRequestedAt,
    string? AgentRejectionReason)
{
    /// <summary>True when the account may create listings and change their content.</summary>
    public bool CanPublish => AgentApproval.CanPublish(Roles, AgentStatus);

    /// <summary>Agent role without approval: "Legacy Agent – Approval Required".</summary>
    public bool IsLegacyAgent => AgentApproval.IsLegacyAgent(Roles, AgentStatus);
}

/// <summary>An account as seen by administrators in the agent review screens.</summary>
public sealed record AgentAccountResponse(
    Guid Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    string? PhoneNumber,
    AgentStatus AgentStatus,
    string? AgentRequestMessage,
    DateTime? AgentRequestedAt,
    DateTime? AgentReviewedAt,
    string? AgentRejectionReason,
    bool IsLegacyAgent);

public interface IAuthService
{
    /// <summary>Creates a Client account, optionally with a pending agent request. Never an Agent or Admin.</summary>
    Task<Result<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the credentials (with lockout) and returns the principal to sign in.
    /// Unknown email and wrong password produce the same error.
    /// </summary>
    Task<Result<ClaimsPrincipal>> CreateLoginPrincipalAsync(LoginRequest request);

    /// <summary>
    /// Exchanges a refresh token for a fresh principal (with the user's current roles). Fails if the
    /// token is expired, tampered with, or was issued before the user's sessions were revoked.
    /// </summary>
    Task<Result<ClaimsPrincipal>> CreateRefreshPrincipalAsync(RefreshRequest request);

    /// <summary>Invalidates every refresh token of the user (all devices).</summary>
    Task<Result> RevokeSessionsAsync(Guid userId);

    Task<UserResponse?> GetUserAsync(Guid userId);

    Task<Result<UserResponse>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);

    /// <summary>Files (or re-files after a rejection) a request to become an agent.</summary>
    Task<Result<UserResponse>> RequestAgentAsync(Guid userId, AgentRequestBody request, CancellationToken cancellationToken);
}

/// <summary>Admin side of the agent approval workflow.</summary>
public interface IAgentAdministration
{
    /// <summary>Accounts in the given status; legacy agents are listed under <see cref="AgentStatus.None"/>.</summary>
    Task<IReadOnlyList<AgentAccountResponse>> ListAsync(AgentStatus? status, CancellationToken cancellationToken);

    /// <summary>Adds the Agent role (keeping Client) and marks the account approved. A phone number is required.</summary>
    Task<Result<AgentAccountResponse>> ApproveAsync(Guid userId, Guid adminId, CancellationToken cancellationToken);

    Task<Result<AgentAccountResponse>> RejectAsync(Guid userId, Guid adminId, string? reason, CancellationToken cancellationToken);
}
