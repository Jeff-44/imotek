using Imotek.Domain.Common;
using Imotek.Domain.Properties;

namespace Imotek.Domain.Requests;

/// <summary>
/// A client asks to visit a property (for rent or for sale) at a given date and time. The agent
/// accepts, declines or proposes another date, which the client then accepts or not. No calendar.
/// </summary>
public sealed class VisitRequest : ClientRequest
{
    /// <summary>How far ahead a visit may be planned.</summary>
    public static readonly TimeSpan MaxAdvance = TimeSpan.FromDays(180);

    // Required by EF Core.
    private VisitRequest()
    {
    }

    private VisitRequest(Guid propertyId, Guid ownerId, Guid? clientId, GuestContact? guest, string? message, bool shareClientPhone, DateTime visitAt, DateTime utcNow)
        : base(propertyId, ownerId, clientId, guest, message, shareClientPhone, utcNow)
    {
        VisitAt = visitAt;
    }

    public override RequestType Type => RequestType.Visit;

    /// <summary>Requested date of the visit (UTC); after an accepted proposal, the agreed date.</summary>
    public DateTime VisitAt { get; private set; }

    /// <summary>Date proposed by the agent, waiting for the client's answer.</summary>
    public DateTime? ProposedVisitAt { get; private set; }

    public static VisitRequest Create(Property property, Guid clientId, string? message, bool shareClientPhone, DateTime visitAt, DateTime utcNow)
    {
        RequestRules.EnsureRequestable(property, transactionType: null);
        EnsureValidDate(nameof(VisitAt), visitAt, utcNow);
        return new VisitRequest(property.Id, property.OwnerId, clientId, null, message, shareClientPhone, visitAt, utcNow);
    }

    /// <summary>A visitor without an account, who leaves their contact details.</summary>
    public static VisitRequest CreateForGuest(Property property, GuestContact guest, string? message, DateTime visitAt, DateTime utcNow)
    {
        RequestRules.EnsureRequestable(property, transactionType: null);
        EnsureValidDate(nameof(VisitAt), visitAt, utcNow);
        return new VisitRequest(property.Id, property.OwnerId, null, guest, message, shareClientPhone: true, visitAt, utcNow);
    }

    public static string? ValidateDate(DateTime visitAt, DateTime utcNow)
    {
        if (visitAt.Kind != DateTimeKind.Utc)
        {
            return "The date must include its time zone.";
        }

        if (visitAt <= utcNow)
        {
            return "The visit must be in the future.";
        }

        return visitAt > utcNow + MaxAdvance ? $"The visit must be within {MaxAdvance.Days} days." : null;
    }

    public void ProposeDate(DateTime proposedAt, string? agentMessage, DateTime utcNow)
    {
        EnsureValidDate(nameof(ProposedVisitAt), proposedAt, utcNow);
        Transition(RequestAction.ProposeDate, agentMessage, utcNow);
        ProposedVisitAt = proposedAt;
    }

    /// <summary>The client agrees with the agent's date, which becomes the visit date.</summary>
    public void AcceptProposal(DateTime utcNow)
    {
        Transition(RequestAction.AcceptProposal, null, utcNow);
        VisitAt = ProposedVisitAt!.Value;
        ProposedVisitAt = null;
    }

    protected override IReadOnlyCollection<RequestStatus> AllowedFrom(RequestAction action) => action switch
    {
        RequestAction.Accept => From(RequestStatus.Pending),
        RequestAction.Decline => From(RequestStatus.Pending, RequestStatus.Rescheduled),
        RequestAction.ProposeDate => From(RequestStatus.Pending, RequestStatus.Rescheduled, RequestStatus.Accepted),
        RequestAction.Close => From(RequestStatus.Accepted),
        RequestAction.AcceptProposal => From(RequestStatus.Rescheduled),
        RequestAction.Cancel => From(RequestStatus.Pending, RequestStatus.Rescheduled, RequestStatus.Accepted),
        _ => [],
    };

    private static void EnsureValidDate(string field, DateTime value, DateTime utcNow)
    {
        if (ValidateDate(value, utcNow) is { } error)
        {
            throw new DomainValidationException(new Dictionary<string, string[]> { [field] = [error] });
        }
    }
}
