using Imotek.API.Configuration;
using Imotek.Application.Common;
using Imotek.Application.Requests;
using Imotek.Domain.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Imotek.API.Controllers;

/// <summary>Rental, purchase and visit requests. Who may see or act on a request is decided by <see cref="ClientRequestService"/>.</summary>
[Authorize]
public sealed class RequestsController(ClientRequestService requests, AgentContactService contacts) : ApiControllerBase
{
    private static readonly Dictionary<string, RequestAction> Actions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["respond"] = RequestAction.Respond,
        ["accept"] = RequestAction.Accept,
        ["decline"] = RequestAction.Decline,
        ["propose-date"] = RequestAction.ProposeDate,
        ["close"] = RequestAction.Close,
        ["cancel"] = RequestAction.Cancel,
        ["accept-proposal"] = RequestAction.AcceptProposal,
    };

    /// <summary>The agent's name, and phone number when the agent agreed to show it. Signed-in users only.</summary>
    [HttpGet("api/properties/{id:guid}/contact")]
    [EnableRateLimiting(ApiSecurity.ContactRateLimit)]
    public async Task<ActionResult<AgentContactResponse>> Contact(Guid id, CancellationToken cancellationToken) =>
        OkOrProblem(await contacts.GetAsync(id, cancellationToken));

    /// <summary>Sends a request (type Rental, Purchase or Visit) about an available property.</summary>
    [HttpPost("api/properties/{id:guid}/requests")]
    [Authorize(Roles = Roles.Client)]
    [EnableRateLimiting(ApiSecurity.ContactRateLimit)]
    public async Task<ActionResult<ClientRequestResponse>> Create(Guid id, CreateRequestBody body, CancellationToken cancellationToken)
    {
        var result = await requests.CreateAsync(id, body, cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : ToProblem(result.Error!);
    }

    /// <summary>Requests the caller sent.</summary>
    [HttpGet("api/requests/mine")]
    public async Task<ActionResult<IReadOnlyList<ClientRequestResponse>>> Mine(CancellationToken cancellationToken) =>
        OkOrProblem(await requests.ListMineAsync(cancellationToken));

    /// <summary>Requests about the caller's properties.</summary>
    [HttpGet("api/requests/received")]
    public async Task<ActionResult<IReadOnlyList<ClientRequestResponse>>> Received(CancellationToken cancellationToken) =>
        OkOrProblem(await requests.ListReceivedAsync(cancellationToken));

    /// <summary>respond, accept, decline, propose-date, close (agent); cancel, accept-proposal (client).</summary>
    [HttpPost("api/requests/{id:guid}/{verb}")]
    public async Task<ActionResult<ClientRequestResponse>> Act(
        Guid id, string verb, [FromBody] RequestActionBody? body, CancellationToken cancellationToken)
    {
        if (!Actions.TryGetValue(verb, out var requestAction))
        {
            return ToProblem(Error.NotFound($"Unknown action '{verb}'."));
        }

        return OkOrProblem(await requests.ActAsync(id, requestAction, body ?? new RequestActionBody(), cancellationToken));
    }
}
