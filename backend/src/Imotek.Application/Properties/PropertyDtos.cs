using Imotek.Domain.Properties;

namespace Imotek.Application.Properties;

/// <summary>
/// Body of create/update requests. There is deliberately no OwnerId: the owner always
/// comes from the authenticated identity.
/// </summary>
public sealed record PropertyRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public PropertyType? PropertyType { get; init; }
    public TransactionType? TransactionType { get; init; }
    public decimal? Price { get; init; }
    public Currency? Currency { get; init; }

    /// <summary>Defaults to <see cref="PropertyService.DefaultCountry"/>.</summary>
    public string? Country { get; init; }

    public string? Department { get; init; }
    public string? City { get; init; }
    public string? Address { get; init; }
    public int? Bedrooms { get; init; }
    public int? Bathrooms { get; init; }
    public decimal? Area { get; init; }

    /// <summary>Defaults to <see cref="PropertyStatus.Draft"/>.</summary>
    public PropertyStatus? Status { get; init; }

    public IReadOnlyList<string>? ImageUrls { get; init; }
}

/// <summary>Query string of the search endpoints.</summary>
public sealed record PropertySearchRequest
{
    public string? Search { get; init; }
    public string? Location { get; init; }
    public string? Department { get; init; }
    public PropertyType? PropertyType { get; init; }
    public TransactionType? TransactionType { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public Currency? Currency { get; init; }

    /// <summary>Ignored by the public search, which only returns available properties.</summary>
    public PropertyStatus? Status { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = PropertyService.DefaultPageSize;
}

public sealed record PropertyResponse(
    Guid Id,
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
    /// <summary>Every photo to display, uploaded ones first.</summary>
    IReadOnlyList<string> ImageUrls,
    /// <summary>Uploaded photos, with the id needed to delete them.</summary>
    IReadOnlyList<PropertyPhotoResponse> Photos,
    /// <summary>External links, as sent in <see cref="PropertyRequest.ImageUrls"/>.</summary>
    IReadOnlyList<string> ExternalImageUrls,
    PropertyStatus Status,
    Guid OwnerId,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static PropertyResponse From(Property p, Func<string, string> publicUrl)
    {
        var photos = p.Images
            .OrderBy(i => i.SortOrder)
            .Select(i => new PropertyPhotoResponse(i.Id, publicUrl(i.StorageKey)))
            .ToList();

        return new(
            p.Id, p.Title, p.Description, p.PropertyType, p.TransactionType, p.Price, p.Currency,
            p.Country, p.Department, p.City, p.Address, p.Bedrooms, p.Bathrooms, p.Area,
            [.. photos.Select(ph => ph.Url), .. p.ImageUrls], photos, p.ImageUrls,
            p.Status, p.OwnerId, p.CreatedAt, p.UpdatedAt);
    }
}

public sealed record PropertyPhotoResponse(Guid Id, string Url);
