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

    /// <summary>The agent's name, and phone number when the agent agreed to show it. Open to visitors.</summary>
    [HttpGet("api/properties/{id:guid}/contact")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiSecurity.ContactRateLimit)]
    public async Task<ActionResult<AgentContactResponse>> Contact(Guid id, CancellationToken cancellationToken) =>
        OkOrProblem(await contacts.GetAsync(id, cancellationToken));

    /// <summary>
    /// Sends a request (type Rental, Purchase or Visit) about an available property. Visitors without an
    /// account may send visits and rentals with their contact details; signed-in users need the Client role.
    /// </summary>
    [HttpPost("api/properties/{id:guid}/requests")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiSecurity.RequestRateLimit)]
    public async Task<ActionResult<ClientRequestResponse>> Create(Guid id, CreateRequestBody body, CancellationToken cancellationToken)
    {
        var result = await requests.CreateAsync(id, body, cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : ToProblem(result.Error!);
    }

    /// <summary>Carries the secret of a visitor's follow-up link, so that it never appears in a URL (and in access logs).</summary>
    public const string TrackingTokenHeader = "X-Tracking-Token";

    /// <summary>A visitor's request, through the secret of their private follow-up link (in <see cref="TrackingTokenHeader"/>).</summary>
    [HttpGet("api/requests/track")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiSecurity.ContactRateLimit)]
    public async Task<ActionResult<ClientRequestResponse>> Track(
        [FromHeader(Name = TrackingTokenHeader)] string? token, CancellationToken cancellationToken) =>
        OkOrProblem(await requests.TrackAsync(token ?? string.Empty, cancellationToken));

    /// <summary>cancel, accept-proposal — for a visitor, through their private follow-up link (in <see cref="TrackingTokenHeader"/>).</summary>
    [HttpPost("api/requests/track/{verb}")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiSecurity.ContactRateLimit)]
    public Task<ActionResult<ClientRequestResponse>> ActAsGuest(
        string verb, [FromHeader(Name = TrackingTokenHeader)] string? token, CancellationToken cancellationToken) =>
        ActAsGuestAsync(token ?? string.Empty, verb, cancellationToken);

    /// <summary>
    /// Deprecated: the token in the path ends up in access logs. Kept for pages still served from an older
    /// build during the transition; same checks and answers as the header endpoints.
    /// </summary>
    [HttpGet("api/requests/track/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiSecurity.ContactRateLimit)]
    public async Task<ActionResult<ClientRequestResponse>> TrackByPath(string token, CancellationToken cancellationToken) =>
        OkOrProblem(await requests.TrackAsync(token, cancellationToken));

    /// <summary>Deprecated, see <see cref="TrackByPath"/>.</summary>
    [HttpPost("api/requests/track/{token}/{verb}")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiSecurity.ContactRateLimit)]
    public Task<ActionResult<ClientRequestResponse>> ActAsGuestByPath(string token, string verb, CancellationToken cancellationToken) =>
        ActAsGuestAsync(token, verb, cancellationToken);

    private async Task<ActionResult<ClientRequestResponse>> ActAsGuestAsync(string token, string verb, CancellationToken cancellationToken) =>
        Actions.TryGetValue(verb, out var action)
            ? OkOrProblem(await requests.ActAsGuestAsync(token, action, cancellationToken))
            : ToProblem(Error.NotFound($"Unknown action '{verb}'."));

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
