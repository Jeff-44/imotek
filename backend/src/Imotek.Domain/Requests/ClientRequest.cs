using Imotek.Domain.Common;

namespace Imotek.Domain.Requests;

public enum RequestType
{
    Rental,
    Purchase,
    Visit,
}

public enum RequestStatus
{
    Pending,

    /// <summary>The agent replied without deciding yet.</summary>
    Responded,

    Accepted,

    /// <summary>Visits only: the agent proposed another date, the client has to accept it.</summary>
    Rescheduled,

    Declined,
    Cancelled,
    Closed,
}

public enum RequestAction
{
    // Agent (owner of the property)
    Respond,
    Accept,
    Decline,
    ProposeDate,
    Close,

    // Client (author of the request)
    Cancel,
    AcceptProposal,
}

/// <summary>Contact details of a visitor who sends a request without an account (already validated and normalized).</summary>
public sealed record GuestContact(string FullName, string Email, string PhoneNumber)
{
    public const int FullNameMaxLength = 150;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 32;
}

/// <summary>
/// What a client asks the owner of a property: to rent it, to buy it, or to visit it.
/// Each kind is its own entity (and table) with its own lifecycle; this base holds what they share.
/// A request is never deleted: declined, cancelled and closed requests stay as history.
/// </summary>
public abstract class ClientRequest
{
    public const int MessageMaxLength = 1000;

    /// <summary>Statuses in which a request still waits for something. One active request per client, property and kind.</summary>
    public static readonly IReadOnlyList<RequestStatus> ActiveStatuses =
        [RequestStatus.Pending, RequestStatus.Responded, RequestStatus.Accepted, RequestStatus.Rescheduled];

    // Required by EF Core.
    protected ClientRequest()
    {
    }

    protected ClientRequest(Guid propertyId, Guid ownerId, Guid clientId, string? message, bool shareClientPhone, DateTime utcNow)
        : this(propertyId, ownerId, clientId, guest: null, message, shareClientPhone, utcNow)
    {
    }

    /// <summary>Either a signed-in client (<paramref name="clientId"/>) or a visitor without an account (<paramref name="guest"/>).</summary>
    protected ClientRequest(Guid propertyId, Guid ownerId, Guid? clientId, GuestContact? guest, string? message, bool shareClientPhone, DateTime utcNow)
    {
        var errors = new Dictionary<string, string[]>();
        if (propertyId == Guid.Empty || ownerId == Guid.Empty || (clientId is null) == (guest is null) || clientId == Guid.Empty)
        {
            errors[nameof(PropertyId)] = ["A request needs a property, its owner, and either a client or a visitor's contact details."];
        }

        if (ownerId == clientId)
        {
            errors[nameof(ClientId)] = ["You cannot send a request about your own property."];
        }

        if (guest is not null)
        {
            RequireText(errors, nameof(GuestName), guest.FullName, GuestContact.FullNameMaxLength);
            RequireText(errors, nameof(GuestEmail), guest.Email, GuestContact.EmailMaxLength);
            RequireText(errors, nameof(GuestPhone), guest.PhoneNumber, GuestContact.PhoneMaxLength);
        }

        AddMessageError(errors, nameof(Message), message);
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        Id = Guid.CreateVersion7();
        PropertyId = propertyId;
        OwnerId = ownerId;
        ClientId = clientId;
        GuestName = guest?.FullName.Trim();
        GuestEmail = guest?.Email.Trim();
        GuestPhone = guest?.PhoneNumber.Trim();
        Message = Clean(message);
        // A visitor gives their number in the form precisely so that the agent can call back.
        ShareClientPhone = guest is not null || shareClientPhone;
        Status = RequestStatus.Pending;
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid PropertyId { get; private set; }

    /// <summary>Owner of the property when the request was made: the only agent who may see and answer it.</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>The signed-in client, or null for a visitor without an account (see the Guest fields).</summary>
    public Guid? ClientId { get; private set; }

    public string? GuestName { get; private set; }
    public string? GuestEmail { get; private set; }
    public string? GuestPhone { get; private set; }

    /// <summary>SHA-256 of the private link given to a visitor to follow their request. Never the link itself.</summary>
    public string? GuestAccessTokenHash { get; private set; }

    public bool IsGuest => ClientId is null;

    public string? Message { get; private set; }

    /// <summary>The client agreed to show their phone number to the owner, for this request only.</summary>
    public bool ShareClientPhone { get; private set; }

    public RequestStatus Status { get; private set; }

    /// <summary>Latest message from the agent.</summary>
    public string? AgentResponse { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public abstract RequestType Type { get; }

    public bool IsActive => ActiveStatuses.Contains(Status);

    public static bool IsClientAction(RequestAction action) => action is RequestAction.Cancel or RequestAction.AcceptProposal;

    /// <summary>True when <paramref name="action"/> is possible from the current status.</summary>
    public bool Allows(RequestAction action) => AllowedFrom(action).Contains(Status);

    /// <summary>Statuses from which <paramref name="action"/> may be taken; empty when this kind does not support it.</summary>
    protected abstract IReadOnlyCollection<RequestStatus> AllowedFrom(RequestAction action);

    /// <summary>
    /// Applies an action that needs nothing but an optional agent message. Visits have their own
    /// methods for the actions that carry a date (<see cref="VisitRequest.ProposeDate"/>, <see cref="VisitRequest.AcceptProposal"/>).
    /// </summary>
    public void Apply(RequestAction action, string? agentMessage, DateTime utcNow)
    {
        if (action is RequestAction.ProposeDate or RequestAction.AcceptProposal)
        {
            throw new InvalidOperationException($"{action} needs a date: use the visit methods.");
        }

        Transition(action, agentMessage, utcNow);
    }

    /// <summary>Visitors only, once: stores the hash of their private follow-up link.</summary>
    public void IssueGuestAccess(string tokenHash)
    {
        if (!IsGuest || GuestAccessTokenHash is not null)
        {
            throw new InvalidOperationException("Only a visitor's request gets a follow-up link, and only once.");
        }

        GuestAccessTokenHash = tokenHash;
    }

    public static void AddMessageError(IDictionary<string, string[]> errors, string field, string? message)
    {
        if (message is not null && message.Trim().Length > MessageMaxLength)
        {
            errors[field] = [$"{field} must be at most {MessageMaxLength} characters."];
        }
    }

    protected void Transition(RequestAction action, string? agentMessage, DateTime utcNow)
    {
        if (!Allows(action))
        {
            throw new InvalidOperationException($"{action} is not possible on a {Type} request that is {Status}.");
        }

        var errors = new Dictionary<string, string[]>();
        AddMessageError(errors, nameof(AgentResponse), agentMessage);
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        Status = action switch
        {
            RequestAction.Respond => RequestStatus.Responded,
            RequestAction.Accept or RequestAction.AcceptProposal => RequestStatus.Accepted,
            RequestAction.Decline => RequestStatus.Declined,
            RequestAction.ProposeDate => RequestStatus.Rescheduled,
            RequestAction.Close => RequestStatus.Closed,
            RequestAction.Cancel => RequestStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        if (!IsClientAction(action) && Clean(agentMessage) is { } response)
        {
            AgentResponse = response;
        }

        UpdatedAt = utcNow;
    }

    private static void RequireText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required."];
        }
        else if (value.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} must be at most {maxLength} characters."];
        }
    }

    protected static IReadOnlyCollection<RequestStatus> From(params RequestStatus[] statuses) => statuses;

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
