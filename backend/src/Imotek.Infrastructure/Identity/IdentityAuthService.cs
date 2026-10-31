using System.Security.Claims;
using Imotek.Application.Auth;
using Imotek.Application.Common;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Imotek.Infrastructure.Identity;

internal sealed class IdentityAuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
    TimeProvider timeProvider,
    IPhoneNumberNormalizer phoneNumbers) : IAuthService
{
    private const string InvalidCredentials = "Invalid email or password.";
    private const string InvalidRefreshToken = "The session has expired. Please sign in again.";

    // Same validation as the refresh endpoint of MapIdentityApi: the token must decrypt,
    // be unexpired, and carry the user's current security stamp.
    public async Task<Result<ClaimsPrincipal>> CreateRefreshPrincipalAsync(RefreshRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Error.Unauthorized(InvalidRefreshToken);
        }

        var protector = bearerTokenOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
        var ticket = protector.Unprotect(request.RefreshToken);

        if (ticket?.Properties.ExpiresUtc is not { } expiresUtc
            || timeProvider.GetUtcNow() >= expiresUtc
            || await signInManager.ValidateSecurityStampAsync(ticket.Principal) is not { } user)
        {
            return Error.Unauthorized(InvalidRefreshToken);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Error.Unauthorized(InvalidRefreshToken);
        }

        return await signInManager.CreateUserPrincipalAsync(user);
    }

    public async Task<Result> RevokeSessionsAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Error.Unauthorized();
        }

        // A new security stamp makes every refresh token issued so far invalid.
        await userManager.UpdateSecurityStampAsync(user);
        return Result.Success();
    }

    public async Task<Result<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        // Legacy "role" field: Client is the default, Agent only files a request, anything else is refused.
        var requestAgent = request.RequestAgent;
        if (request.Role is { } legacyRole)
        {
            if (string.Equals(legacyRole, Roles.Agent, StringComparison.OrdinalIgnoreCase))
            {
                requestAgent = true;
            }
            else if (!string.Equals(legacyRole, Roles.Client, StringComparison.OrdinalIgnoreCase))
            {
                errors[nameof(request.Role)] = [$"Role must be one of: {string.Join(", ", Roles.SelfRegistrable)}."];
            }
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors[nameof(request.Email)] = ["Email is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors[nameof(request.Password)] = ["Password is required."];
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            errors[nameof(request.FullName)] = ["FullName is required."];
        }
        else if (request.FullName.Trim().Length > ApplicationUser.FullNameMaxLength)
        {
            errors[nameof(request.FullName)] = [$"FullName must be at most {ApplicationUser.FullNameMaxLength} characters."];
        }

        var phone = ValidatePhone(request.PhoneNumber, required: requestAgent, errors);
        if (requestAgent)
        {
            ValidateAgentRequest(request.PhonePublicConsent, request.AgentRequestMessage, errors);
        }

        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        var email = request.Email!.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName!.Trim(),
            PhoneNumber = phone,
            PhonePublicConsent = requestAgent && request.PhonePublicConsent,
        };
        if (requestAgent)
        {
            MarkAgentRequested(user, request.AgentRequestMessage);
        }

        var created = await userManager.CreateAsync(user, request.Password!);
        if (!created.Succeeded)
        {
            return ToError(created);
        }

        // Every account is a Client; the Agent role only comes from an admin approval.
        var addedToRole = await userManager.AddToRoleAsync(user, Roles.Client);
        if (!addedToRole.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return ToError(addedToRole);
        }

        return ToResponse(user, [Roles.Client]);
    }

    public async Task<Result<UserResponse>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Error.Unauthorized();
        }

        var roles = (await userManager.GetRolesAsync(user)).ToList();
        var isAgentAccount = roles.Contains(Roles.Agent) || user.AgentStatus is AgentStatus.Pending or AgentStatus.Approved;

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            errors[nameof(request.FullName)] = ["FullName is required."];
        }
        else if (request.FullName.Trim().Length > ApplicationUser.FullNameMaxLength)
        {
            errors[nameof(request.FullName)] = [$"FullName must be at most {ApplicationUser.FullNameMaxLength} characters."];
        }

        // Agents (and pending agents) must keep a reachable number and their consent to show it.
        var phone = ValidatePhone(request.PhoneNumber, required: isAgentAccount, errors);
        if (isAgentAccount && user.AgentStatus is AgentStatus.Pending or AgentStatus.Approved && !request.PhonePublicConsent)
        {
            errors[nameof(request.PhonePublicConsent)] = ["Agents must agree to show their phone number on their listings."];
        }

        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        user.FullName = request.FullName!.Trim();
        user.PhoneNumber = phone;
        user.PhonePublicConsent = phone is not null && request.PhonePublicConsent;

        var updated = await userManager.UpdateAsync(user);
        return updated.Succeeded ? ToResponse(user, roles) : ToError(updated);
    }

    public async Task<Result<UserResponse>> RequestAgentAsync(Guid userId, AgentRequestBody request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Error.Unauthorized();
        }

        var roles = (await userManager.GetRolesAsync(user)).ToList();
        if (AgentApproval.CheckCanRequest(roles, user.AgentStatus) is { } refused)
        {
            return refused;
        }

        var errors = new Dictionary<string, string[]>();
        var phone = ValidatePhone(request.PhoneNumber, required: true, errors);
        ValidateAgentRequest(request.PhonePublicConsent, request.Message, errors);
        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        user.PhoneNumber = phone;
        user.PhonePublicConsent = true;
        MarkAgentRequested(user, request.Message);

        var updated = await userManager.UpdateAsync(user);
        return updated.Succeeded ? ToResponse(user, roles) : ToError(updated);
    }

    /// <summary>Null when empty and not required; otherwise E.164, or a validation error.</summary>
    private string? ValidatePhone(string? input, bool required, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            if (required)
            {
                errors[nameof(RegisterRequest.PhoneNumber)] = ["PhoneNumber is required for agents."];
            }

            return null;
        }

        if (phoneNumbers.Normalize(input) is { } normalized)
        {
            return normalized.E164;
        }

        errors[nameof(RegisterRequest.PhoneNumber)] = ["PhoneNumber is not a valid phone number for its country."];
        return null;
    }

    private static void ValidateAgentRequest(bool consent, string? message, Dictionary<string, string[]> errors)
    {
        if (!consent)
        {
            errors[nameof(RegisterRequest.PhonePublicConsent)] = ["Agents must agree to show their phone number on their listings."];
        }

        if (message?.Trim().Length > AgentApproval.MessageMaxLength)
        {
            errors[nameof(AgentRequestBody.Message)] = [$"Message must be at most {AgentApproval.MessageMaxLength} characters."];
        }
    }

    private void MarkAgentRequested(ApplicationUser user, string? message)
    {
        user.AgentStatus = AgentStatus.Pending;
        user.AgentRequestedAt = timeProvider.GetUtcNow().UtcDateTime;
        user.AgentReviewedAt = null;
        user.AgentRequestMessage = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        // The previous rejection reason, if any, is kept until the new request is decided.
    }

    internal static UserResponse ToResponse(ApplicationUser user, IReadOnlyList<string> roles) => new(
        user.Id,
        user.Email ?? string.Empty,
        user.FullName,
        roles,
        user.PhoneNumber,
        user.PhonePublicConsent,
        user.AgentStatus,
        user.AgentRequestedAt,
        user.AgentStatus == AgentStatus.Rejected ? user.AgentRejectionReason : null);

    public async Task<Result<ClaimsPrincipal>> CreateLoginPrincipalAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return Error.Unauthorized(InvalidCredentials);
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return Error.Unauthorized(InvalidCredentials);
        }

        var check = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            return Error.Unauthorized("Too many failed attempts. Try again later.");
        }

        if (!check.Succeeded)
        {
            return Error.Unauthorized(InvalidCredentials);
        }

        return await signInManager.CreateUserPrincipalAsync(user);
    }

    public async Task<UserResponse?> GetUserAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        return ToResponse(user, roles.ToList());
    }

    internal static Error ToError(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return Error.Conflict("An account with this email already exists.");
        }

        var errors = result.Errors
            .GroupBy(e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? nameof(RegisterRequest.Password) : nameof(RegisterRequest.Email))
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return Error.Validation(errors);
    }
}
