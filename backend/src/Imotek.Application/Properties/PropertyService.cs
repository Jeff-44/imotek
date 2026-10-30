using Imotek.Application.Common;
using Imotek.Application.Images;
using Imotek.Domain.Properties;

namespace Imotek.Application.Properties;

/// <summary>
/// Property use cases. Every authorization rule lives here, so it holds no matter
/// which endpoint (agent, admin, public) calls in.
/// </summary>
public sealed class PropertyService(
    IPropertyRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IFileStorage storage,
    IImageProcessor imageProcessor)
{
    public const string DefaultCountry = "Haïti";
    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 50;

    private bool IsAdmin => currentUser.IsInRole(Roles.Admin);

    private bool IsAgent => currentUser.UserId is not null && currentUser.IsInRole(Roles.Agent);

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<Result<PropertyResponse>> CreateAsync(PropertyRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized();
        }

        if (!IsAgent)
        {
            return Error.Forbidden("Only agents and owners can publish properties.");
        }

        var details = ToDetails(request);
        if (details.Error is not null)
        {
            return details.Error;
        }

        var property = Property.Create(userId, details.Value, UtcNow);
        repository.Add(property);
        await repository.SaveChangesAsync(cancellationToken);

        return ToResponse(property);
    }

    public async Task<Result<PropertyResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var property = await repository.GetByIdAsync(id, cancellationToken);
        if (property is null || !CanView(property))
        {
            return Error.NotFound();
        }

        return ToResponse(property);
    }

    public async Task<Result<PropertyResponse>> UpdateAsync(Guid id, PropertyRequest request, CancellationToken cancellationToken)
    {
        var property = await repository.GetByIdAsync(id, cancellationToken);
        if (Deny(property) is { } error)
        {
            return error;
        }

        var details = ToDetails(request);
        if (details.Error is not null)
        {
            return details.Error;
        }

        property!.Update(details.Value, UtcNow);
        await repository.SaveChangesAsync(cancellationToken);

        return ToResponse(property);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var property = await repository.GetByIdAsync(id, cancellationToken);
        if (Deny(property) is { } error)
        {
            return error;
        }

        var storageKeys = property!.Images.Select(i => i.StorageKey).ToList();
        repository.Remove(property);
        await repository.SaveChangesAsync(cancellationToken);

        // Files go only once the rows are gone, so a failed save never leaves broken photos.
        foreach (var key in storageKeys)
        {
            await storage.DeleteAsync(key, cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result<PropertyResponse>> AddImageAsync(Guid id, UploadedFile file, CancellationToken cancellationToken)
    {
        var property = await repository.GetByIdAsync(id, cancellationToken);
        if (Deny(property) is { } error)
        {
            return error;
        }

        if (file.Length <= 0)
        {
            return Error.Validation("File", "The file is empty.");
        }

        if (file.Length > ImageRules.MaxUploadBytes)
        {
            return Error.Validation("File", $"The file must be at most {ImageRules.MaxUploadBytes / (1024 * 1024)} MB.");
        }

        if (!property!.CanAddImage)
        {
            return Error.Validation("File", $"A property can have at most {Property.MaxImages} images.");
        }

        var content = await ReadBoundedAsync(file.Content, ImageRules.MaxUploadBytes, cancellationToken);
        if (content is null)
        {
            return Error.Validation("File", $"The file must be at most {ImageRules.MaxUploadBytes / (1024 * 1024)} MB.");
        }

        // The real format comes from the bytes; the declared name and content type are ignored.
        var image = imageProcessor.Sanitize(content);
        if (image is null)
        {
            return Error.Validation("File", "Only JPEG, PNG and WebP images are accepted.");
        }

        var imageId = Guid.CreateVersion7();
        var key = $"properties/{property.Id:N}/{imageId:N}{image.Extension}";
        await storage.SaveAsync(key, image.Content, image.ContentType, cancellationToken);

        try
        {
            property.AddImage(
                PropertyImage.Create(imageId, key, image.ContentType, image.Content.Length, property.NextImageSortOrder, UtcNow),
                UtcNow);
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        return ToResponse(property);
    }

    public async Task<Result<PropertyResponse>> DeleteImageAsync(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        var property = await repository.GetByIdAsync(id, cancellationToken);
        if (Deny(property) is { } error)
        {
            return error;
        }

        var image = property!.RemoveImage(imageId, UtcNow);
        if (image is null)
        {
            return Error.NotFound("The image was not found.");
        }

        await repository.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(image.StorageKey, cancellationToken);

        return ToResponse(property);
    }

    /// <summary>Public catalogue: only available properties, whoever asks.</summary>
    public Task<Result<PagedResult<PropertyResponse>>> SearchPublicAsync(PropertySearchRequest request, CancellationToken cancellationToken) =>
        SearchAsync(request, filter => filter with { Status = PropertyStatus.Available, OwnerId = null }, cancellationToken);

    /// <summary>The caller's own properties, in every status.</summary>
    public Task<Result<PagedResult<PropertyResponse>>> SearchMineAsync(PropertySearchRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Task.FromResult<Result<PagedResult<PropertyResponse>>>(Error.Unauthorized());
        }

        if (!IsAgent)
        {
            return Task.FromResult<Result<PagedResult<PropertyResponse>>>(Error.Forbidden());
        }

        return SearchAsync(request, filter => filter with { OwnerId = userId }, cancellationToken);
    }

    /// <summary>Every property, every owner, every status.</summary>
    public Task<Result<PagedResult<PropertyResponse>>> SearchAllAsync(PropertySearchRequest request, CancellationToken cancellationToken)
    {
        if (!IsAdmin)
        {
            return Task.FromResult<Result<PagedResult<PropertyResponse>>>(Error.Forbidden());
        }

        return SearchAsync(request, filter => filter, cancellationToken);
    }

    private async Task<Result<PagedResult<PropertyResponse>>> SearchAsync(
        PropertySearchRequest request,
        Func<PropertyFilter, PropertyFilter> scope,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Page < 1)
        {
            errors[nameof(request.Page)] = ["Page must be 1 or greater."];
        }

        if (request.PageSize is < 1 or > MaxPageSize)
        {
            errors[nameof(request.PageSize)] = [$"PageSize must be between 1 and {MaxPageSize}."];
        }

        if (request.MinPrice < 0)
        {
            errors[nameof(request.MinPrice)] = ["MinPrice cannot be negative."];
        }

        if (request.MinPrice > request.MaxPrice)
        {
            errors[nameof(request.MaxPrice)] = ["MaxPrice must be greater than or equal to MinPrice."];
        }

        // HTG and USD amounts are never compared with each other: there is no
        // configured exchange rate, so a price range only makes sense in one currency.
        if ((request.MinPrice is not null || request.MaxPrice is not null) && request.Currency is null)
        {
            errors[nameof(request.Currency)] = ["Currency is required when filtering by price."];
        }

        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        var filter = scope(new PropertyFilter
        {
            Status = request.Status,
            Search = Normalize(request.Search),
            Location = Normalize(request.Location),
            Department = Normalize(request.Department),
            PropertyType = request.PropertyType,
            TransactionType = request.TransactionType,
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice,
            Currency = request.Currency,
        });

        var page = await repository.SearchAsync(filter, request.Page, request.PageSize, cancellationToken);
        return page.Map(ToResponse);
    }

    private bool CanView(Property property) =>
        property.IsPubliclyVisible || CanManage(property);

    private bool CanManage(Property property) =>
        IsAdmin || (IsAgent && property.IsOwnedBy(currentUser.UserId!.Value));

    /// <summary>
    /// Null when the caller may manage the property. Otherwise 403 if the caller can
    /// already see it, and 404 if it must stay hidden (missing, draft, rented, sold).
    /// </summary>
    private Error? Deny(Property? property)
    {
        if (currentUser.UserId is null)
        {
            return Error.Unauthorized();
        }

        if (property is null)
        {
            return Error.NotFound();
        }

        if (CanManage(property))
        {
            return null;
        }

        return CanView(property) ? Error.Forbidden("You can only manage your own properties.") : Error.NotFound();
    }

    private static Result<PropertyDetails> ToDetails(PropertyRequest request)
    {
        var missing = new Dictionary<string, string[]>();
        if (request.PropertyType is null)
        {
            missing[nameof(request.PropertyType)] = ["PropertyType is required."];
        }

        if (request.TransactionType is null)
        {
            missing[nameof(request.TransactionType)] = ["TransactionType is required."];
        }

        if (request.Price is null)
        {
            missing[nameof(request.Price)] = ["Price is required."];
        }

        if (request.Currency is null)
        {
            missing[nameof(request.Currency)] = ["Currency is required."];
        }

        var details = new PropertyDetails(
            Title: request.Title ?? string.Empty,
            Description: request.Description ?? string.Empty,
            PropertyType: request.PropertyType ?? default,
            TransactionType: request.TransactionType ?? default,
            Price: request.Price ?? 0,
            Currency: request.Currency ?? default,
            Country: string.IsNullOrWhiteSpace(request.Country) ? DefaultCountry : request.Country,
            Department: request.Department ?? string.Empty,
            City: request.City ?? string.Empty,
            Address: request.Address ?? string.Empty,
            Bedrooms: request.Bedrooms,
            Bathrooms: request.Bathrooms,
            Area: request.Area,
            Status: request.Status ?? PropertyStatus.Draft,
            ImageUrls: request.ImageUrls ?? []);

        // Missing-field errors take precedence over the domain message for the same field.
        var errors = Property.Validate(details)
            .Where(e => !missing.ContainsKey(e.Key))
            .Concat(missing)
            .ToDictionary(e => e.Key, e => e.Value);

        return errors.Count > 0 ? Error.Validation(errors) : details;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private PropertyResponse ToResponse(Property property) => PropertyResponse.From(property, storage.GetPublicUrl);

    /// <summary>Reads at most <paramref name="maxBytes"/>; null if the stream is longer, whatever Length claimed.</summary>
    private static async Task<byte[]?> ReadBoundedAsync(Stream stream, long maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
