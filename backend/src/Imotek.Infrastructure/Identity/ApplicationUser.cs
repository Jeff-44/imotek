using Imotek.Application.Auth;
using Microsoft.AspNetCore.Identity;

namespace Imotek.Infrastructure.Identity;

/// <summary>
/// Identity user. The phone number uses Identity's own <see cref="IdentityUser{TKey}.PhoneNumber"/>
/// column, always stored in E.164 form.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const int FullNameMaxLength = 150;

    public string FullName { get; set; } = string.Empty;

    /// <summary>The user agreed that their phone number is shown on their public listings.</summary>
    public bool PhonePublicConsent { get; set; }

    public AgentStatus AgentStatus { get; set; } = AgentStatus.None;
    public DateTime? AgentRequestedAt { get; set; }
    public DateTime? AgentReviewedAt { get; set; }
    public string? AgentRequestMessage { get; set; }
    public string? AgentRejectionReason { get; set; }
}
