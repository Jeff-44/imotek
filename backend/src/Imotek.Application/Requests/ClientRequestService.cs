using Imotek.Application.Common;
using Imotek.Application.Properties;
using Imotek.Domain.Notifications;
using Imotek.Domain.Properties;
using Imotek.Domain.Requests;

namespace Imotek.Application.Requests;

/// <summary>
/// Rental, purchase and visit requests. A client sees only their own requests, an agent only the
/// requests about their own properties; anything else answers 404 so other requests stay invisible.
/// </summary>
public sealed class ClientRequestService(
    IClientRequestRepository requests,
    IPropertyRepository properties,
    IUserDirectory users,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private static readonly RequestAction[] AgentActions =
        [RequestAction.Respond, RequestAction.Accept, RequestAction.Decline, RequestAction.ProposeDate, RequestAction.Close];

    private static readonly RequestAction[] ClientActions = [RequestAction.Cancel, RequestAction.AcceptProposal];

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<Result<ClientRequestResponse>> CreateAsync(Guid propertyId, CreateRequestBody body, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } clientId)
        {
            return Error.Unauthorized();
        }

        if (!currentUser.IsInRole(Roles.Client))
        {
            return Error.Forbidden("Only client accounts can send requests.");
        }

        var property = await properties.GetByIdAsync(propertyId, cancellationToken);
        if (property is null || !property.IsPubliclyVisible)
        {
            return Error.NotFound("The property was not found or is no longer available.");
        }

        if (property.IsOwnedBy(clientId))
        {
            return Error.Forbidden("You cannot send a request about your own property.");
        }

        var errors = new Dictionary<string, string[]>();
        ClientRequest.AddMessageError(errors, nameof(body.Message), body.Message);
        switch (body.Type)
        {
            case null:
                errors[nameof(body.Type)] = ["Type is required."];
                break;
            case { } unknown when !Enum.IsDefined(unknown):
                errors[nameof(body.Type)] = ["Type has an invalid value."];
                break;
            case RequestType.Rental when property.TransactionType != TransactionType.Rent:
                errors[nameof(body.Type)] = ["Rental requests are only possible on properties for rent."];
                break;
            case RequestType.Purchase when property.TransactionType != TransactionType.Sale:
                errors[nameof(body.Type)] = ["Purchase requests are only possible on properties for sale."];
                break;
            case RequestType.Visit when body.VisitAt is null:
                errors[nameof(body.VisitAt)] = ["VisitAt is required."];
                break;
            case RequestType.Visit when VisitRequest.ValidateDate(body.VisitAt!.Value.UtcDateTime, UtcNow) is { } dateError:
                errors[nameof(body.VisitAt)] = [dateError];
                break;
        }

        if (body.ShareClientPhone)
        {
            var me = (await users.GetAsync([clientId], cancellationToken)).GetValueOrDefault(clientId);
            if (string.IsNullOrWhiteSpace(me?.PhoneNumber))
            {
                errors[nameof(body.ShareClientPhone)] = ["Add a phone number to your profile before sharing it."];
            }
        }

        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        var type = body.Type!.Value;
        if (await requests.HasActiveAsync(type, property.Id, clientId, cancellationToken))
        {
            return DuplicateError(type);
        }

        ClientRequest request = type switch
        {
            RequestType.Rental => RentalRequest.Create(property, clientId, body.Message, body.ShareClientPhone, UtcNow),
            RequestType.Purchase => PurchaseRequest.Create(property, clientId, body.Message, body.ShareClientPhone, UtcNow),
            _ => VisitRequest.Create(property, clientId, body.Message, body.ShareClientPhone, body.VisitAt!.Value.UtcDateTime, UtcNow),
        };

        requests.Add(request);
        requests.AddNotification(Notification.About(request, property.OwnerId, NotificationKind.RequestReceived, property.Title, UtcNow));
        if (!await requests.SaveChangesAsync(cancellationToken))
        {
            return DuplicateError(type);
        }

        return (await ToResponsesAsync([request], asAgent: false, cancellationToken))[0];
    }

    /// <summary>Requests the caller sent, every kind and status.</summary>
    public async Task<Result<IReadOnlyList<ClientRequestResponse>>> ListMineAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized();
        }

        var list = await requests.ListByClientAsync(userId, cancellationToken);
        return Result<IReadOnlyList<ClientRequestResponse>>.From(await ToResponsesAsync(list, asAgent: false, cancellationToken));
    }

    /// <summary>Requests about the caller's properties, every kind and status.</summary>
    public async Task<Result<IReadOnlyList<ClientRequestResponse>>> ListReceivedAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized();
        }

        var list = await requests.ListByOwnerAsync(userId, cancellationToken);
        return Result<IReadOnlyList<ClientRequestResponse>>.From(await ToResponsesAsync(list, asAgent: true, cancellationToken));
    }

    public async Task<Result<ClientRequestResponse>> ActAsync(Guid id, RequestAction action, RequestActionBody body, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized();
        }

        var byClient = ClientRequest.IsClientAction(action);
        var request = await requests.GetByIdAsync(id, cancellationToken);
        if (request is null || (byClient ? request.ClientId : request.OwnerId) != userId)
        {
            return Error.NotFound("The request was not found.");
        }

        if (!request.Allows(action))
        {
            return Error.Conflict($"This action is not possible on a {request.Type.ToString().ToLowerInvariant()} request that is {request.Status.ToString().ToLowerInvariant()}.");
        }

        var errors = new Dictionary<string, string[]>();
        if (!byClient)
        {
            ClientRequest.AddMessageError(errors, nameof(body.Message), body.Message);
        }

        if (action == RequestAction.ProposeDate)
        {
            if (body.ProposedVisitAt is not { } proposed)
            {
                errors[nameof(body.ProposedVisitAt)] = ["ProposedVisitAt is required."];
            }
            else if (VisitRequest.ValidateDate(proposed.UtcDateTime, UtcNow) is { } dateError)
            {
                errors[nameof(body.ProposedVisitAt)] = [dateError];
            }
        }

        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        switch (action)
        {
            case RequestAction.ProposeDate:
                ((VisitRequest)request).ProposeDate(body.ProposedVisitAt!.Value.UtcDateTime, body.Message, UtcNow);
                break;
            case RequestAction.AcceptProposal:
                ((VisitRequest)request).AcceptProposal(UtcNow);
                break;
            default:
                request.Apply(action, byClient ? null : body.Message, UtcNow);
                break;
        }

        var property = (await properties.GetByIdsAsync([request.PropertyId], cancellationToken)).GetValueOrDefault(request.PropertyId);
        requests.AddNotification(Notification.About(
            request,
            byClient ? request.OwnerId : request.ClientId,
            NotificationFor(action),
            property?.Title ?? string.Empty,
            UtcNow));
        await requests.SaveChangesAsync(cancellationToken);

        return (await ToResponsesAsync([request], asAgent: !byClient, cancellationToken))[0];
    }

    private static Error DuplicateError(RequestType type) =>
        Error.Conflict($"You already have an open {type.ToString().ToLowerInvariant()} request for this property.");

    private static NotificationKind NotificationFor(RequestAction action) => action switch
    {
        RequestAction.Respond => NotificationKind.RequestResponded,
        RequestAction.Accept => NotificationKind.RequestAccepted,
        RequestAction.Decline => NotificationKind.RequestDeclined,
        RequestAction.ProposeDate => NotificationKind.VisitRescheduled,
        RequestAction.Close => NotificationKind.RequestClosed,
        RequestAction.Cancel => NotificationKind.RequestCancelled,
        RequestAction.AcceptProposal => NotificationKind.ProposalAccepted,
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    /// <summary>
    /// The agent sees the client's name and email, and their phone only when shared for that request.
    /// The client sees the agent's name only: the number goes through the contact endpoint and its consent.
    /// </summary>
    private async Task<IReadOnlyList<ClientRequestResponse>> ToResponsesAsync(
        IReadOnlyList<ClientRequest> list, bool asAgent, CancellationToken cancellationToken)
    {
        var propertyMap = await properties.GetByIdsAsync(list.Select(r => r.PropertyId).Distinct().ToList(), cancellationToken);
        var people = await users.GetAsync(
            list.Select(r => asAgent ? r.ClientId : r.OwnerId).Distinct().ToList(), cancellationToken);
        var actions = asAgent ? AgentActions : ClientActions;

        return list.Select(r =>
        {
            var property = propertyMap.GetValueOrDefault(r.PropertyId);
            var person = people.GetValueOrDefault(asAgent ? r.ClientId : r.OwnerId);
            var party = person is null
                ? null
                : asAgent
                    ? new RequestParty(person.FullName, person.Email, r.ShareClientPhone ? person.PhoneNumber : null)
                    : new RequestParty(person.FullName, null, null);
            var visit = r as VisitRequest;

            return new ClientRequestResponse(
                r.Id,
                r.Type,
                r.Status,
                r.PropertyId,
                property?.Title ?? string.Empty,
                property?.City ?? string.Empty,
                r.Message,
                r.ShareClientPhone,
                r.AgentResponse,
                visit?.VisitAt,
                visit?.ProposedVisitAt,
                r.CreatedAt,
                r.UpdatedAt,
                asAgent ? party : null,
                asAgent ? null : party,
                actions.Where(r.Allows).ToList());
        }).ToList();
    }
}
