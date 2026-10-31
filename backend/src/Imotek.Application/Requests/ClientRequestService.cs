using Imotek.Application.Common;
using Imotek.Application.Properties;
using Imotek.Domain.Notifications;
using Imotek.Domain.Properties;
using Imotek.Domain.Requests;

namespace Imotek.Application.Requests;

/// <summary>
/// Rental, purchase and visit requests. A client sees only their own requests, an agent only the
/// requests about their own properties; anything else answers 404 so other requests stay invisible.
/// Visitors without an account may ask for a visit or a rental with their contact details; they follow
/// their request through a private link (only its hash is stored). Purchases need an account.
/// </summary>
public sealed class ClientRequestService(
    IClientRequestRepository requests,
    IPropertyRepository properties,
    IUserDirectory users,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IPhoneNumberNormalizer phoneNumbers)
{
    private static readonly RequestAction[] AgentActions =
        [RequestAction.Respond, RequestAction.Accept, RequestAction.Decline, RequestAction.ProposeDate, RequestAction.Close];

    private static readonly RequestAction[] ClientActions = [RequestAction.Cancel, RequestAction.AcceptProposal];

    private static readonly RentalDetails NoRentalDetails = new(null, null, null, null);

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<Result<ClientRequestResponse>> CreateAsync(Guid propertyId, CreateRequestBody body, CancellationToken cancellationToken)
    {
        var clientId = currentUser.UserId;
        if (clientId is not null && !currentUser.IsInRole(Roles.Client))
        {
            return Error.Forbidden("Only client accounts can send requests.");
        }

        if (clientId is null && body.Type == RequestType.Purchase)
        {
            return Error.Unauthorized("Sign in to send a purchase request.");
        }

        var property = await properties.GetByIdAsync(propertyId, cancellationToken);
        if (property is null || !property.IsPubliclyVisible)
        {
            return Error.NotFound("The property was not found or is no longer available.");
        }

        if (clientId is { } id && property.IsOwnedBy(id))
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

        var rental = new RentalDetails(body.CurrentAddress, body.DurationMonths, body.MoveInDate, body.Occupants);
        if (body.Type == RequestType.Rental)
        {
            foreach (var (field, messages) in RentalDetails.Validate(rental, DateOnly.FromDateTime(UtcNow), required: clientId is null))
            {
                errors[field] = messages;
            }
        }

        var guest = clientId is null ? ValidateGuest(body, errors) : null;
        if (clientId is { } signedIn && body.ShareClientPhone)
        {
            var me = (await users.GetAsync([signedIn], cancellationToken)).GetValueOrDefault(signedIn);
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
        var duplicate = clientId is { } client
            ? await requests.HasActiveAsync(type, property.Id, client, cancellationToken)
            : await requests.HasActiveGuestAsync(type, property.Id, guest!.Email, cancellationToken);
        if (duplicate)
        {
            return DuplicateError(type);
        }

        ClientRequest request = (type, clientId) switch
        {
            (RequestType.Rental, { } c) => RentalRequest.Create(property, c, body.Message, body.ShareClientPhone, UtcNow, rental == NoRentalDetails ? null : rental),
            (RequestType.Rental, null) => RentalRequest.CreateForGuest(property, guest!, body.Message, rental, UtcNow),
            (RequestType.Purchase, { } c) => PurchaseRequest.Create(property, c, body.Message, body.ShareClientPhone, UtcNow),
            (_, { } c) => VisitRequest.Create(property, c, body.Message, body.ShareClientPhone, body.VisitAt!.Value.UtcDateTime, UtcNow),
            _ => VisitRequest.CreateForGuest(property, guest!, body.Message, body.VisitAt!.Value.UtcDateTime, UtcNow),
        };

        string? trackingToken = null;
        if (request.IsGuest)
        {
            trackingToken = GuestAccessToken.Create();
            request.IssueGuestAccess(GuestAccessToken.Hash(trackingToken));
        }

        requests.Add(request);
        requests.AddNotification(Notification.About(request, property.OwnerId, NotificationKind.RequestReceived, property.Title, UtcNow));
        if (!await requests.SaveChangesAsync(cancellationToken))
        {
            return DuplicateError(type);
        }

        var response = (await ToResponsesAsync([request], asAgent: false, cancellationToken))[0];
        return response with { TrackingToken = trackingToken };
    }

    /// <summary>A visitor's request, through the private link they were given.</summary>
    public async Task<Result<ClientRequestResponse>> TrackAsync(string token, CancellationToken cancellationToken)
    {
        var request = await FindByTokenAsync(token, cancellationToken);
        return request is null
            ? Error.NotFound("The request was not found.")
            : (await ToResponsesAsync([request], asAgent: false, cancellationToken))[0];
    }

    /// <summary>Cancel, or accept the agent's new visit date, through the private link.</summary>
    public async Task<Result<ClientRequestResponse>> ActAsGuestAsync(string token, RequestAction action, CancellationToken cancellationToken)
    {
        var request = await FindByTokenAsync(token, cancellationToken);
        if (request is null || !ClientRequest.IsClientAction(action))
        {
            return Error.NotFound("The request was not found.");
        }

        if (!request.Allows(action))
        {
            return Error.Conflict($"This action is not possible on a {request.Type.ToString().ToLowerInvariant()} request that is {request.Status.ToString().ToLowerInvariant()}.");
        }

        if (action == RequestAction.AcceptProposal)
        {
            ((VisitRequest)request).AcceptProposal(UtcNow);
        }
        else
        {
            request.Apply(action, null, UtcNow);
        }

        var property = (await properties.GetByIdsAsync([request.PropertyId], cancellationToken)).GetValueOrDefault(request.PropertyId);
        requests.AddNotification(Notification.About(request, request.OwnerId, NotificationFor(action), property?.Title ?? string.Empty, UtcNow));
        await requests.SaveChangesAsync(cancellationToken);
        return (await ToResponsesAsync([request], asAgent: false, cancellationToken))[0];
    }

    private async Task<ClientRequest?> FindByTokenAsync(string token, CancellationToken cancellationToken) =>
        GuestAccessToken.IsWellFormed(token)
            ? await requests.GetByGuestTokenHashAsync(GuestAccessToken.Hash(token), cancellationToken)
            : null;

    /// <summary>Name, a valid email and a valid phone number (stored in E.164), or validation errors.</summary>
    private GuestContact? ValidateGuest(CreateRequestBody body, Dictionary<string, string[]> errors)
    {
        var name = body.FullName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            errors[nameof(body.FullName)] = ["FullName is required."];
        }
        else if (name.Length > GuestContact.FullNameMaxLength)
        {
            errors[nameof(body.FullName)] = [$"FullName must be at most {GuestContact.FullNameMaxLength} characters."];
        }

        var email = body.Email?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            errors[nameof(body.Email)] = ["Email is required."];
        }
        else if (!IsEmail(email))
        {
            errors[nameof(body.Email)] = ["Email is not a valid email address."];
        }

        var phone = phoneNumbers.Normalize(body.PhoneNumber);
        if (string.IsNullOrWhiteSpace(body.PhoneNumber))
        {
            errors[nameof(body.PhoneNumber)] = ["PhoneNumber is required."];
        }
        else if (phone is null)
        {
            errors[nameof(body.PhoneNumber)] = ["PhoneNumber is not a valid phone number for its country."];
        }

        return errors.ContainsKey(nameof(body.FullName)) || errors.ContainsKey(nameof(body.Email)) || phone is null
            ? null
            : new GuestContact(name!, email!.ToLowerInvariant(), phone.E164);
    }

    /// <summary>One address, with a dot in its domain (name@host.tld), within the length limit.</summary>
    private static bool IsEmail(string email) =>
        email.Length <= GuestContact.EmailMaxLength
        && System.Net.Mail.MailAddress.TryCreate(email, out var parsed)
        && parsed.Address == email
        && parsed.Host.Contains('.', StringComparison.Ordinal);

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

        // A visitor has no account to notify: they follow the request through their private link.
        var recipient = byClient ? request.OwnerId : request.ClientId;
        if (recipient is { } recipientId)
        {
            var property = (await properties.GetByIdsAsync([request.PropertyId], cancellationToken)).GetValueOrDefault(request.PropertyId);
            requests.AddNotification(Notification.About(request, recipientId, NotificationFor(action), property?.Title ?? string.Empty, UtcNow));
        }

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
    /// The agent sees the client's name and email, and their phone only when shared for that request;
    /// for a visitor, the contact details they typed in the form. The client sees the agent's name
    /// only: the number goes through the contact endpoint and its consent.
    /// </summary>
    private async Task<IReadOnlyList<ClientRequestResponse>> ToResponsesAsync(
        IReadOnlyList<ClientRequest> list, bool asAgent, CancellationToken cancellationToken)
    {
        var propertyMap = await properties.GetByIdsAsync(list.Select(r => r.PropertyId).Distinct().ToList(), cancellationToken);
        var people = await users.GetAsync(
            list.Select(r => asAgent ? r.ClientId : r.OwnerId).OfType<Guid>().Distinct().ToList(), cancellationToken);
        var actions = asAgent ? AgentActions : ClientActions;

        return list.Select(r =>
        {
            var property = propertyMap.GetValueOrDefault(r.PropertyId);
            var person = (asAgent ? r.ClientId : r.OwnerId) is { } personId ? people.GetValueOrDefault(personId) : null;
            var party = asAgent && r.IsGuest
                ? new RequestParty(r.GuestName!, r.GuestEmail, r.GuestPhone)
                : person is null
                    ? null
                    : asAgent
                        ? new RequestParty(person.FullName, person.Email, r.ShareClientPhone ? person.PhoneNumber : null)
                        : new RequestParty(person.FullName, null, null);
            var visit = r as VisitRequest;
            var rental = r as RentalRequest;

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
                actions.Where(r.Allows).ToList())
            {
                IsGuest = r.IsGuest,
                CurrentAddress = rental?.CurrentAddress,
                DurationMonths = rental?.DurationMonths,
                MoveInDate = rental?.MoveInDate,
                Occupants = rental?.Occupants,
            };
        }).ToList();
    }
}
