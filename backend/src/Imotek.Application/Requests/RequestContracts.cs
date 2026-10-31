using Imotek.Domain.Notifications;
using Imotek.Domain.Requests;

namespace Imotek.Application.Requests;

public interface IClientRequestRepository
{
    /// <summary>Returns a tracked request of any kind, or null.</summary>
    Task<ClientRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Newest first, every kind.</summary>
    Task<IReadOnlyList<ClientRequest>> ListByClientAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>Newest first, every kind.</summary>
    Task<IReadOnlyList<ClientRequest>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken);

    Task<bool> HasActiveAsync(RequestType type, Guid propertyId, Guid clientId, CancellationToken cancellationToken);

    void Add(ClientRequest request);

    void AddNotification(Notification notification);

    /// <summary>Saves everything pending. False when another active request of the same kind already exists (unique index).</summary>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface INotificationRepository
{
    Task<IReadOnlyList<Notification>> ListAsync(Guid userId, int take, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Returns a tracked notification, or null.</summary>
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<int> MarkAllReadAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Body of <c>POST /api/properties/{id}/requests</c>.</summary>
public sealed record CreateRequestBody
{
    public RequestType? Type { get; init; }
    public string? Message { get; init; }

    /// <summary>Shows the client's profile phone number to the owner, for this request only.</summary>
    public bool ShareClientPhone { get; init; }

    /// <summary>Visits only. Must carry its time zone offset.</summary>
    public DateTimeOffset? VisitAt { get; init; }
}

/// <summary>Body of the action endpoints. <see cref="ProposedVisitAt"/> is required to propose a date.</summary>
public sealed record RequestActionBody
{
    public string? Message { get; init; }
    public DateTimeOffset? ProposedVisitAt { get; init; }
}

/// <summary>Who the request is with: the client (seen by the agent) or the agent (seen by the client).</summary>
public sealed record RequestParty(string FullName, string? Email, string? PhoneNumber);

public sealed record ClientRequestResponse(
    Guid Id,
    RequestType Type,
    RequestStatus Status,
    Guid PropertyId,
    string PropertyTitle,
    string PropertyCity,
    string? Message,
    bool ShareClientPhone,
    string? AgentResponse,
    DateTime? VisitAt,
    DateTime? ProposedVisitAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    RequestParty? Client,
    RequestParty? Agent,
    /// <summary>What the caller may do now, so the interface shows only valid buttons.</summary>
    IReadOnlyList<RequestAction> AllowedActions);

public sealed record NotificationResponse(
    Guid Id,
    NotificationKind Kind,
    Guid RequestId,
    RequestType RequestType,
    Guid PropertyId,
    string PropertyTitle,
    DateTime CreatedAt,
    bool IsRead);

public sealed record NotificationList(IReadOnlyList<NotificationResponse> Items, int UnreadCount);

/// <summary>Contact details of a listing's agent. The number is null unless the agent agreed to show it.</summary>
public sealed record AgentContactResponse(string AgentName, string? PhoneNumber);
