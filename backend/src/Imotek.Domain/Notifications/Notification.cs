using Imotek.Domain.Requests;

namespace Imotek.Domain.Notifications;

public enum NotificationKind
{
    // To the agent
    RequestReceived,
    RequestCancelled,
    ProposalAccepted,

    // To the client
    RequestResponded,
    RequestAccepted,
    RequestDeclined,
    RequestClosed,
    VisitRescheduled,
}

/// <summary>
/// An in-app notice about a request. It stores what happened, not a sentence: the interface writes
/// the text in the user's language. The property title is copied so the notice still reads well later.
/// </summary>
public sealed class Notification
{
    public const int TitleMaxLength = Properties.Property.TitleMaxLength;

    // Required by EF Core.
    private Notification()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public NotificationKind Kind { get; private set; }
    public Guid RequestId { get; private set; }
    public RequestType RequestType { get; private set; }
    public Guid PropertyId { get; private set; }
    public string PropertyTitle { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static Notification About(ClientRequest request, Guid recipientId, NotificationKind kind, string propertyTitle, DateTime utcNow) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = recipientId,
        Kind = kind,
        RequestId = request.Id,
        RequestType = request.Type,
        PropertyId = request.PropertyId,
        PropertyTitle = propertyTitle.Length > TitleMaxLength ? propertyTitle[..TitleMaxLength] : propertyTitle,
        CreatedAt = utcNow,
    };

    public void MarkRead(DateTime utcNow) => ReadAt ??= utcNow;
}
