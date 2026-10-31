using Imotek.Domain.Properties;

namespace Imotek.Domain.Requests;

/// <summary>
/// A client asks to buy a property listed for sale. It opens a conversation, it is not a sale:
/// it never changes the property's status; the owner marks the property Sold when the sale happens.
/// </summary>
public sealed class PurchaseRequest : ClientRequest
{
    // Required by EF Core.
    private PurchaseRequest()
    {
    }

    private PurchaseRequest(Guid propertyId, Guid ownerId, Guid clientId, string? message, bool shareClientPhone, DateTime utcNow)
        : base(propertyId, ownerId, clientId, message, shareClientPhone, utcNow)
    {
    }

    public override RequestType Type => RequestType.Purchase;

    public static PurchaseRequest Create(Property property, Guid clientId, string? message, bool shareClientPhone, DateTime utcNow)
    {
        RequestRules.EnsureRequestable(property, TransactionType.Sale);
        return new PurchaseRequest(property.Id, property.OwnerId, clientId, message, shareClientPhone, utcNow);
    }

    /// <summary>Pending → Responded → Declined | Closed; the client may cancel while it is open.</summary>
    protected override IReadOnlyCollection<RequestStatus> AllowedFrom(RequestAction action) => action switch
    {
        RequestAction.Respond => From(RequestStatus.Pending, RequestStatus.Responded),
        RequestAction.Decline => From(RequestStatus.Pending, RequestStatus.Responded),
        RequestAction.Close => From(RequestStatus.Pending, RequestStatus.Responded),
        RequestAction.Cancel => From(RequestStatus.Pending, RequestStatus.Responded),
        _ => [],
    };
}
