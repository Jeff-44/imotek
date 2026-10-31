namespace Imotek.Application.Common;

/// <summary>A phone number checked against its country's numbering plan, in E.164 form (e.g. +50937123456).</summary>
public sealed record NormalizedPhoneNumber(string E164, string RegionCode);

public interface IPhoneNumberNormalizer
{
    /// <summary>Numbers without a leading "+" are read as Haitian numbers.</summary>
    public const string DefaultRegion = "HT";

    /// <summary>Null when the input is not a valid number for its country.</summary>
    NormalizedPhoneNumber? Normalize(string? input);
}

/// <summary>Whether a user may publish (approved agent). Read from the account store, not from the token.</summary>
public interface IAgentAccess
{
    Task<bool> CanPublishAsync(Guid userId, CancellationToken cancellationToken);
}
