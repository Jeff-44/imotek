using Imotek.Application.Auth;
using Imotek.Application.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Imotek.Infrastructure.Identity;

/// <summary>Admin decisions on agent requests. The controller restricts it to admins; it also refuses self-decisions.</summary>
internal sealed class IdentityAgentAdministration(UserManager<ApplicationUser> userManager, TimeProvider timeProvider)
    : IAgentAdministration, IAgentAccess
{
    public async Task<IReadOnlyList<AgentAccountResponse>> ListAsync(AgentStatus? status, CancellationToken cancellationToken)
    {
        var candidates = new Dictionary<Guid, ApplicationUser>();

        if (status is null or AgentStatus.Pending or AgentStatus.Approved or AgentStatus.Rejected)
        {
            var query = userManager.Users.Where(u => u.AgentStatus != AgentStatus.None);
            if (status is { } wanted)
            {
                query = query.Where(u => u.AgentStatus == wanted);
            }

            foreach (var user in await query.ToListAsync(cancellationToken))
            {
                candidates[user.Id] = user;
            }
        }

        // Legacy agents: the Agent role without any decision.
        if (status is null or AgentStatus.None)
        {
            foreach (var user in await userManager.GetUsersInRoleAsync(Roles.Agent))
            {
                if (user.AgentStatus == AgentStatus.None)
                {
                    candidates[user.Id] = user;
                }
            }
        }

        var result = new List<AgentAccountResponse>();
        foreach (var user in candidates.Values.OrderBy(u => u.AgentRequestedAt ?? DateTime.MaxValue).ThenBy(u => u.Email))
        {
            result.Add(ToResponse(user, (await userManager.GetRolesAsync(user)).ToList()));
        }

        return result;
    }

    public async Task<Result<AgentAccountResponse>> ApproveAsync(Guid userId, Guid adminId, CancellationToken cancellationToken)
    {
        var (user, roles, error) = await LoadForDecisionAsync(userId, adminId);
        if (error is not null)
        {
            return error;
        }

        if (string.IsNullOrWhiteSpace(user!.PhoneNumber) || !user.PhonePublicConsent)
        {
            return Error.Validation(nameof(user.PhoneNumber),
                "The account needs a valid phone number, with consent to show it, before it can be approved.");
        }

        user.AgentStatus = AgentStatus.Approved;
        user.AgentReviewedAt = timeProvider.GetUtcNow().UtcDateTime;
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            return IdentityAuthService.ToError(updated);
        }

        // The Agent role is added next to Client, never instead of it.
        if (!roles.Contains(Roles.Agent))
        {
            var added = await userManager.AddToRoleAsync(user, Roles.Agent);
            if (!added.Succeeded)
            {
                return IdentityAuthService.ToError(added);
            }

            roles.Add(Roles.Agent);

            // Roles changed: existing refresh tokens are revoked, the next sign-in carries the new role.
            await userManager.UpdateSecurityStampAsync(user);
        }

        return ToResponse(user, roles);
    }

    public async Task<Result<AgentAccountResponse>> RejectAsync(Guid userId, Guid adminId, string? reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation("Reason", "A reason is required to reject an agent request.");
        }

        if (reason.Trim().Length > AgentApproval.ReasonMaxLength)
        {
            return Error.Validation("Reason", $"Reason must be at most {AgentApproval.ReasonMaxLength} characters.");
        }

        var (user, roles, error) = await LoadForDecisionAsync(userId, adminId);
        if (error is not null)
        {
            return error;
        }

        // Roles are left untouched: a rejected legacy agent keeps its data, but cannot publish.
        user!.AgentStatus = AgentStatus.Rejected;
        user.AgentReviewedAt = timeProvider.GetUtcNow().UtcDateTime;
        user.AgentRejectionReason = reason.Trim();
        var updated = await userManager.UpdateAsync(user);
        return updated.Succeeded ? ToResponse(user, roles) : IdentityAuthService.ToError(updated);
    }

    public async Task<bool> CanPublishAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is not null && AgentApproval.CanPublish((await userManager.GetRolesAsync(user)).ToList(), user.AgentStatus);
    }

    private async Task<(ApplicationUser? User, List<string> Roles, Error? Error)> LoadForDecisionAsync(Guid userId, Guid adminId)
    {
        if (userId == adminId)
        {
            return (null, [], Error.Forbidden("You cannot decide on your own agent request."));
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return (null, [], Error.NotFound("The account was not found."));
        }

        var roles = (await userManager.GetRolesAsync(user)).ToList();
        return AgentApproval.CheckCanDecide(roles, user.AgentStatus) is { } refused
            ? (null, roles, refused)
            : (user, roles, null);
    }

    private static AgentAccountResponse ToResponse(ApplicationUser user, IReadOnlyList<string> roles) => new(
        user.Id,
        user.Email ?? string.Empty,
        user.FullName,
        roles,
        user.PhoneNumber,
        user.AgentStatus,
        user.AgentRequestMessage,
        user.AgentRequestedAt,
        user.AgentReviewedAt,
        user.AgentRejectionReason,
        AgentApproval.IsLegacyAgent(roles, user.AgentStatus));
}
