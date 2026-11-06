using System.Collections.Concurrent;
using System.Security.Claims;
using Imotek.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Imotek.Infrastructure.Identity;

/// <summary>
/// Current security stamp of each user, kept for a short time so that checking access tokens does not
/// query the database on every request. Entries are dropped as soon as this instance changes a stamp
/// (logout, agent approval); a change made elsewhere is seen at most <see cref="Lifetime"/> later.
/// </summary>
public sealed class SecurityStampCache(TimeProvider timeProvider, IOptions<SecurityStampCacheOptions> options)
{
    private readonly ConcurrentDictionary<Guid, (string? Stamp, DateTimeOffset ExpiresAt)> _entries = new();

    public TimeSpan Lifetime { get; } = TimeSpan.FromSeconds(options.Value.Seconds);

    public bool TryGet(Guid userId, out string? stamp)
    {
        if (_entries.TryGetValue(userId, out var entry) && entry.ExpiresAt > timeProvider.GetUtcNow())
        {
            stamp = entry.Stamp;
            return true;
        }

        stamp = null;
        return false;
    }

    public void Set(Guid userId, string? stamp) => _entries[userId] = (stamp, timeProvider.GetUtcNow() + Lifetime);

    public void Invalidate(Guid userId) => _entries.TryRemove(userId, out _);
}

public sealed class SecurityStampCacheOptions
{
    public const string SectionName = "Auth:SecurityStampCache";

    /// <summary>Longest time an access token keeps working after its session was revoked by another instance.</summary>
    public int Seconds { get; set; } = 30;
}

/// <summary>
/// Access tokens are self-contained and would stay valid until they expire. Before the bearer handler
/// accepts one, this compares the security stamp it carries with the user's current one: after a logout
/// (on any device) or an approval, authentication fails exactly as for an expired token. Protected
/// endpoints answer 401 and public endpoints serve the caller anonymously, as with an expired token.
/// </summary>
internal sealed class AccessTokenStampValidator(ImotekDbContext db, SecurityStampCache cache, IOptions<IdentityOptions> identityOptions)
{
    public const string RevokedMessage = "The session was revoked.";

    /// <summary>Hooks the check into the bearer scheme, keeping any handler already configured.</summary>
    public static void Attach(BearerTokenOptions options)
    {
        var previous = options.Events.OnMessageReceived;
        options.Events.OnMessageReceived = async context =>
        {
            await previous(context);
            if (context.Result is not null)
            {
                return;
            }

            var token = context.Token ?? BearerTokenFrom(context.Request.Headers.Authorization.ToString());
            // Unreadable or expired tokens are left to the handler, which refuses them the usual way.
            if (token is null || options.BearerTokenProtector.Unprotect(token) is not { } ticket)
            {
                return;
            }

            var validator = context.HttpContext.RequestServices.GetRequiredService<AccessTokenStampValidator>();
            if (!await validator.IsCurrentAsync(ticket.Principal))
            {
                context.Fail(RevokedMessage);
            }
        };
    }

    public async Task<bool> IsCurrentAsync(ClaimsPrincipal principal)
    {
        var claims = identityOptions.Value.ClaimsIdentity;
        if (!Guid.TryParse(principal.FindFirstValue(claims.UserIdClaimType), out var userId)
            || principal.FindFirstValue(claims.SecurityStampClaimType) is not { Length: > 0 } tokenStamp)
        {
            return false;
        }

        if (!cache.TryGet(userId, out var currentStamp))
        {
            // A deleted account reads as null: its tokens are refused too.
            currentStamp = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.SecurityStamp)
                .FirstOrDefaultAsync();
            cache.Set(userId, currentStamp);
        }

        return string.Equals(currentStamp, tokenStamp, StringComparison.Ordinal);
    }

    private static string? BearerTokenFrom(string authorization) =>
        authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) && authorization.Length > 7
            ? authorization[7..].Trim()
            : null;
}
