using Imotek.Domain.Common;
using Imotek.Domain.Properties;

namespace Imotek.Domain.Requests;

/// <summary>What a prospective tenant tells the owner. Required for visitors without an account, optional otherwise.</summary>
public sealed record RentalDetails(string? CurrentAddress, int? DurationMonths, DateOnly? MoveInDate, int? Occupants)
{
    public const int CurrentAddressMaxLength = 250;
    public const int MaxDurationMonths = 120;
    public const int MaxOccupants = 50;

    /// <summary>How far ahead the move-in date may be.</summary>
    public const int MaxMoveInDays = 365;

    public static IReadOnlyDictionary<string, string[]> Validate(RentalDetails details, DateOnly today, bool required)
    {
        var errors = new Dictionary<string, string[]>();
        if (details.CurrentAddress?.Trim().Length > CurrentAddressMaxLength)
        {
            errors[nameof(CurrentAddress)] = [$"{nameof(CurrentAddress)} must be at most {CurrentAddressMaxLength} characters."];
        }

        if (details.DurationMonths is null && required)
        {
            errors[nameof(DurationMonths)] = [$"{nameof(DurationMonths)} is required."];
        }
        else if (details.DurationMonths is < 1 or > MaxDurationMonths)
        {
            errors[nameof(DurationMonths)] = [$"{nameof(DurationMonths)} must be between 1 and {MaxDurationMonths}."];
        }

        if (details.MoveInDate is null && required)
        {
            errors[nameof(MoveInDate)] = [$"{nameof(MoveInDate)} is required."];
        }
        else if (details.MoveInDate is { } date && (date < today || date > today.AddDays(MaxMoveInDays)))
        {
            errors[nameof(MoveInDate)] = [$"The move-in date must be between today and {MaxMoveInDays} days from now."];
        }

        if (details.Occupants is < 1 or > MaxOccupants)
        {
            errors[nameof(Occupants)] = [$"{nameof(Occupants)} must be between 1 and {MaxOccupants}."];
        }

        return errors;
    }
}

/// <summary>A client asks to rent a property listed for rent. Accepting it does not change the property's status.</summary>
public sealed class RentalRequest : ClientRequest
{
    // Required by EF Core.
    private RentalRequest()
    {
    }

    private RentalRequest(Guid propertyId, Guid ownerId, Guid? clientId, GuestContact? guest, string? message, bool shareClientPhone, RentalDetails? details, DateTime utcNow)
        : base(propertyId, ownerId, clientId, guest, message, shareClientPhone, utcNow)
    {
        CurrentAddress = string.IsNullOrWhiteSpace(details?.CurrentAddress) ? null : details.CurrentAddress.Trim();
        DurationMonths = details?.DurationMonths;
        MoveInDate = details?.MoveInDate;
        Occupants = details?.Occupants;
    }

    public override RequestType Type => RequestType.Rental;

    public string? CurrentAddress { get; private set; }
    public int? DurationMonths { get; private set; }
    public DateOnly? MoveInDate { get; private set; }
    public int? Occupants { get; private set; }

    public static RentalRequest Create(Property property, Guid clientId, string? message, bool shareClientPhone, DateTime utcNow, RentalDetails? details = null)
    {
        RequestRules.EnsureRequestable(property, TransactionType.Rent);
        EnsureValid(details, utcNow, required: false);
        return new RentalRequest(property.Id, property.OwnerId, clientId, null, message, shareClientPhone, details, utcNow);
    }

    /// <summary>A visitor without an account: contact details and rental details are required.</summary>
    public static RentalRequest CreateForGuest(Property property, GuestContact guest, string? message, RentalDetails details, DateTime utcNow)
    {
        RequestRules.EnsureRequestable(property, TransactionType.Rent);
        EnsureValid(details, utcNow, required: true);
        return new RentalRequest(property.Id, property.OwnerId, null, guest, message, shareClientPhone: true, details, utcNow);
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

    private static void EnsureValid(RentalDetails? details, DateTime utcNow, bool required)
    {
        if (details is null && !required)
        {
            return;
        }

        var errors = RentalDetails.Validate(details ?? new RentalDetails(null, null, null, null), DateOnly.FromDateTime(utcNow), required);
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }
    }
}
