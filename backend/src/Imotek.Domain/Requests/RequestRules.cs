using Imotek.Domain.Common;
using Imotek.Domain.Properties;

namespace Imotek.Domain.Requests;

internal static class RequestRules
{
    /// <summary>A request can only be made on an available property, of the right transaction type when one is needed.</summary>
    public static void EnsureRequestable(Property property, TransactionType? transactionType)
    {
        if (property.Status != PropertyStatus.Available)
        {
            throw new InvalidOperationException("Requests can only be made on available properties.");
        }

        if (transactionType is { } expected && property.TransactionType != expected)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                ["Type"] = [expected == TransactionType.Rent
                    ? "Rental requests are only possible on properties for rent."
                    : "Purchase requests are only possible on properties for sale."],
            });
        }
    }
}
