namespace Imotek.Domain.Properties;

public enum PropertyType
{
    House,
    Apartment,
    Land,
    CommercialSpace,
    Office,
    Other,
}

public enum TransactionType
{
    Rent,
    Sale,
}

public enum PropertyStatus
{
    Draft,
    Available,
    Rented,
    Sold,

    /// <summary>Withdrawn by its owner or an admin. Kept, with its history, but hidden from the public.</summary>
    Archived,
}

public enum Currency
{
    HTG,
    USD,
}
