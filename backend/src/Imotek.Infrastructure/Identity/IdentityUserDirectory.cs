using Imotek.Application.Auth;
using Imotek.Application.Common;
using Imotek.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Imotek.Infrastructure.Identity;

/// <summary>Reads names, emails, phones and agent approval straight from the Identity tables (no duplicated fields).</summary>
internal sealed class IdentityUserDirectory(ImotekDbContext db) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<Guid, UserSummary>> GetAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, UserSummary>();
        }

        var users = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email, u.PhoneNumber, u.PhonePublicConsent, u.AgentStatus })
            .ToListAsync(cancellationToken);

        var agents = await (
                from userRole in db.UserRoles
                join role in db.Roles on userRole.RoleId equals role.Id
                where role.Name == Roles.Agent && ids.Contains(userRole.UserId)
                select userRole.UserId)
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            u => u.Id,
            u => new UserSummary(
                u.Id,
                u.FullName,
                u.Email ?? string.Empty,
                u.PhoneNumber,
                u.PhonePublicConsent,
                IsApprovedAgent: agents.Contains(u.Id) && u.AgentStatus == AgentStatus.Approved));
    }
}
