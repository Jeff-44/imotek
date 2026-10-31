namespace Imotek.Application.Common;

/// <summary>What the application needs to know about an account, read from the account store.</summary>
public sealed record UserSummary(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool PhonePublicConsent,
    bool IsApprovedAgent);

public interface IUserDirectory
{
    /// <summary>Accounts that exist among <paramref name="ids"/>, by id.</summary>
    Task<IReadOnlyDictionary<Guid, UserSummary>> GetAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}
