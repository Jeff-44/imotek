using System.Security.Claims;
using Imotek.Application.Common;

namespace Imotek.API.Services;

/// <summary>Reads the caller from the authenticated principal, never from the request body.</summary>
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid? UserId =>
        Principal?.Identity?.IsAuthenticated == true
        && Guid.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    public bool IsInRole(string role) => UserId is not null && Principal!.IsInRole(role);
}
