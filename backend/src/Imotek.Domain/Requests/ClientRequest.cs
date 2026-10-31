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
    {
        var errors = new Dictionary<string, string[]>();
        if (propertyId == Guid.Empty || ownerId == Guid.Empty || clientId == Guid.Empty)
        {
            errors[nameof(PropertyId)] = ["A request needs a property, its owner and a client."];
        }

        if (ownerId == clientId)
        {
            errors[nameof(ClientId)] = ["You cannot send a request about your own property."];
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
        Message = Clean(message);
        ShareClientPhone = shareClientPhone;
        Status = RequestStatus.Pending;
        CreatedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public Guid Id { get; private set; }
    public Guid PropertyId { get; private set; }

    /// <summary>Owner of the property when the request was made: the only agent who may see and answer it.</summary>
    public Guid OwnerId { get; private set; }

    public Guid ClientId { get; private set; }
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

    protected static IReadOnlyCollection<RequestStatus> From(params RequestStatus[] statuses) => statuses;

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
