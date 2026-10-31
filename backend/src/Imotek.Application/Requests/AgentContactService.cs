using Imotek.Application.Common;
using Imotek.Application.Properties;

namespace Imotek.Application.Requests;

/// <summary>
/// The agent's phone number for a listing, kept out of every property DTO. It is given only to
/// signed-in users, for an available listing, and only while the agent is approved and consents.
/// Consent is read on every call, so withdrawing it hides the number at once.
/// </summary>
public sealed class AgentContactService(IPropertyRepository properties, IUserDirectory users, ICurrentUser currentUser)
{
    public async Task<Result<AgentContactResponse>> GetAsync(Guid propertyId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            return Error.Unauthorized();
        }

        var property = (await properties.GetByIdsAsync([propertyId], cancellationToken)).GetValueOrDefault(propertyId);
        if (property is null || !property.IsPubliclyVisible)
        {
            return Error.NotFound();
        }

        var owner = (await users.GetAsync([property.OwnerId], cancellationToken)).GetValueOrDefault(property.OwnerId);
        if (owner is null)
        {
            return Error.NotFound();
        }

        var phone = owner.IsApprovedAgent && owner.PhonePublicConsent && !string.IsNullOrWhiteSpace(owner.PhoneNumber)
            ? owner.PhoneNumber
            : null;
        return new AgentContactResponse(owner.FullName, phone);
    }
}
