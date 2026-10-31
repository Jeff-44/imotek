using Imotek.Domain.Properties;

namespace Imotek.Domain.Requests;

/// <summary>A client asks to rent a property listed for rent. Accepting it does not change the property's status.</summary>
public sealed class RentalRequest : ClientRequest
{
    // Required by EF Core.
    private RentalRequest()
    {
    }

    private RentalRequest(Guid propertyId, Guid ownerId, Guid clientId, string? message, bool shareClientPhone, DateTime utcNow)
        : base(propertyId, ownerId, clientId, message, shareClientPhone, utcNow)
    {
    }

    public override RequestType Type => RequestType.Rental;

    public static RentalRequest Create(Property property, Guid clientId, string? message, bool shareClientPhone, DateTime utcNow)
    {
        RequestRules.EnsureRequestable(property, TransactionType.Rent);
        return new RentalRequest(property.Id, property.OwnerId, clientId, message, shareClientPhone, utcNow);
    }

    protected override IReadOnlyCollection<RequestStatus> AllowedFrom(RequestAction action) => action switch
    {
        RequestAction.Respond => From(RequestStatus.Pending, RequestStatus.Responded),
        RequestAction.Accept => From(RequestStatus.Pending, RequestStatus.Responded),
        RequestAction.Decline => From(RequestStatus.Pending, RequestStatus.Responded),
        RequestAction.Close => From(RequestStatus.Responded, RequestStatus.Accepted),
        RequestAction.Cancel => From(RequestStatus.Pending, RequestStatus.Responded, RequestStatus.Accepted),
        _ => [],
    };
}
