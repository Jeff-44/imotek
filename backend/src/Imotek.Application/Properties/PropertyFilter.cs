using Imotek.Domain.Common;
using Imotek.Domain.Properties;

namespace Imotek.Application.Properties;

/// <summary>Normalized search criteria. Built by <see cref="PropertyService"/>, never directly from user input.</summary>
public sealed record PropertyFilter
{
    public Guid? OwnerId { get; init; }
    public PropertyStatus? Status { get; init; }
    public string? Search { get; init; }
    public string? Location { get; init; }
    public string? Department { get; init; }
    public PropertyType? PropertyType { get; init; }
    public TransactionType? TransactionType { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public Currency? Currency { get; init; }
}

public static class PropertyQueryExtensions
{
    /// <summary>
    /// Pure LINQ so the same rules run against EF Core (translated to SQL) and against
    /// in-memory collections in unit tests. Text filters compare against the normalized
    /// keys stored on <see cref="Property"/>, so they ignore case and accents.
    /// </summary>
    public static IQueryable<Property> ApplyFilter(this IQueryable<Property> query, PropertyFilter filter)
    {
        if (filter.OwnerId is { } ownerId)
        {
            query = query.Where(p => p.OwnerId == ownerId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(p => p.Status == status);
        }

        if (SearchText.Normalize(filter.Search) is { Length: > 0 } search)
        {
            query = query.Where(p => p.SearchKey.Contains(search));
        }

        if (SearchText.Normalize(filter.Location) is { Length: > 0 } location)
        {
            query = query.Where(p => p.LocationKey.Contains(location));
        }

        if (SearchText.Normalize(filter.Department) is { Length: > 0 } department)
        {
            query = query.Where(p => p.DepartmentKey == department);
        }

        if (filter.PropertyType is { } propertyType)
        {
            query = query.Where(p => p.PropertyType == propertyType);
        }

        if (filter.TransactionType is { } transactionType)
        {
            query = query.Where(p => p.TransactionType == transactionType);
        }

        if (filter.Currency is { } currency)
        {
            query = query.Where(p => p.Currency == currency);
        }

        if (filter.MinPrice is { } minPrice)
        {
            query = query.Where(p => p.Price >= minPrice);
        }

        if (filter.MaxPrice is { } maxPrice)
        {
            query = query.Where(p => p.Price <= maxPrice);
        }

        return query;
    }

    public static IQueryable<Property> OrderByNewest(this IQueryable<Property> query) =>
        query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id);
}
