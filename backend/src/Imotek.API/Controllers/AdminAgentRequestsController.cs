using Imotek.Application.Auth;
using Imotek.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Imotek.API.Controllers;

[Route("api/admin/agent-requests")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminAgentRequestsController(IAgentAdministration agents, ICurrentUser currentUser) : ApiControllerBase
{
    /// <summary>
    /// Accounts in the agent workflow. ?status=Pending (default), Approved, Rejected, or None for
    /// legacy agents (Agent role without approval); ?status=All for everything.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentAccountResponse>>> List(
        [FromQuery] string? status, CancellationToken cancellationToken)
    {
        AgentStatus? filter = AgentStatus.Pending;
        if (string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            filter = null;
        }
        else if (status is not null)
        {
            if (!Enum.TryParse<AgentStatus>(status, ignoreCase: true, out var parsed))
            {
                return ToProblem(Error.Validation("Status", "Status must be Pending, Approved, Rejected, None or All."));
            }

            filter = parsed;
        }

        return Ok(await agents.ListAsync(filter, cancellationToken));
    }

    [HttpPost("{userId:guid}/approve")]
    public async Task<ActionResult<AgentAccountResponse>> Approve(Guid userId, CancellationToken cancellationToken) =>
        OkOrProblem(await agents.ApproveAsync(userId, currentUser.UserId!.Value, cancellationToken));

    [HttpPost("{userId:guid}/reject")]
    public async Task<ActionResult<AgentAccountResponse>> Reject(
        Guid userId, RejectAgentRequestBody request, CancellationToken cancellationToken) =>
        OkOrProblem(await agents.RejectAsync(userId, currentUser.UserId!.Value, request.Reason, cancellationToken));
}
