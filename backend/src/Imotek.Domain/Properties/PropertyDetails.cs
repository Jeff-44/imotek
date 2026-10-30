namespace Imotek.Domain.Properties;

/// <summary>The editable part of a <see cref="Property"/>.</summary>
public sealed record PropertyDetails(
    string Title,
    string Description,
    PropertyType PropertyType,
    TransactionType TransactionType,
    decimal Price,
    Currency Currency,
    string Country,
    string Department,
    string City,
    string Address,
    int? Bedrooms,
    int? Bathrooms,
    decimal? Area,
    PropertyStatus Status,
    IReadOnlyList<string> ImageUrls);
