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
}

public enum Currency
{
    HTG,
    USD,
}
