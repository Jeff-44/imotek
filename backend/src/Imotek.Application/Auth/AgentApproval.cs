using Imotek.Application.Common;

namespace Imotek.Application.Auth;

/// <summary>Where a user stands in the "become an agent" workflow.</summary>
public enum AgentStatus
{
    /// <summary>Never asked. Also the state of accounts that had the Agent role before the workflow existed.</summary>
    None,
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// Rules of the agent approval workflow. Pure functions, so they are unit tested on their own
/// and applied identically wherever accounts are changed.
/// </summary>
public static class AgentApproval
{
    public const int MessageMaxLength = 1000;
    public const int ReasonMaxLength = 500;

    /// <summary>
    /// An account that holds the Agent role without having been approved: it predates the workflow.
    /// It may manage what it already owns, but not publish or edit content, until an admin decides.
    /// </summary>
    public static bool IsLegacyAgent(IReadOnlyCollection<string> roles, AgentStatus status) =>
        roles.Contains(Roles.Agent) && status != AgentStatus.Approved;

    /// <summary>Only an approved agent may create listings or change their content.</summary>
    public static bool CanPublish(IReadOnlyCollection<string> roles, AgentStatus status) =>
        roles.Contains(Roles.Agent) && status == AgentStatus.Approved;

    public static Error? CheckCanRequest(IReadOnlyCollection<string> roles, AgentStatus status) => status switch
    {
        _ when roles.Contains(Roles.Admin) => Error.Forbidden("Administrators cannot request the agent role."),
        AgentStatus.Pending => Error.Conflict("An agent request is already pending."),
        AgentStatus.Approved => Error.Conflict("This account is already an approved agent."),
        _ => null, // None (including legacy agents) or Rejected: a new request is allowed.
    };

    /// <summary>Pending requests can be decided; so can legacy agents, which an admin reviews directly.</summary>
    public static Error? CheckCanDecide(IReadOnlyCollection<string> roles, AgentStatus status) =>
        status == AgentStatus.Pending || IsLegacyAgent(roles, status) && status == AgentStatus.None
            ? null
            : Error.Conflict("There is no agent request to decide for this account.");
}
