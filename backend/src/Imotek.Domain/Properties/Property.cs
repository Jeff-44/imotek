using Imotek.Domain.Common;

namespace Imotek.Domain.Properties;

public sealed class Property
{
    public const int TitleMaxLength = 150;
    public const int DescriptionMaxLength = 4000;
    public const int LocationMaxLength = 100;
    public const int AddressMaxLength = 250;
    public const int ImageUrlMaxLength = 2048;
    public const int MaxImages = 10;
    public const int MaxRooms = 100;
    public const decimal MaxPrice = 1_000_000_000_000m;

    private List<string> _imageUrls = [];
    private readonly List<PropertyImage> _images = [];

    // Required by EF Core.
    private Property()
    {
    }

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public PropertyType PropertyType { get; private set; }
    public TransactionType TransactionType { get; private set; }
    public decimal Price { get; private set; }
    public Currency Currency { get; private set; }
    public string Country { get; private set; } = string.Empty;
    public string Department { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public int? Bedrooms { get; private set; }
    public int? Bathrooms { get; private set; }
    public decimal? Area { get; private set; }
    public PropertyStatus Status { get; private set; }
    /// <summary>External photo links (legacy). Uploaded photos are in <see cref="Images"/>.</summary>
    public IReadOnlyList<string> ImageUrls => _imageUrls;

    /// <summary>Uploaded photos, in display order.</summary>
    public IReadOnlyList<PropertyImage> Images => _images;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Normalized title and description (see <see cref="SearchText"/>).</summary>
    public string SearchKey { get; private set; } = string.Empty;

    /// <summary>Normalized city, department and address.</summary>
    public string LocationKey { get; private set; } = string.Empty;

    /// <summary>Normalized department, for exact matching.</summary>
    public string DepartmentKey { get; private set; } = string.Empty;

    /// <summary>True when the property may be shown to anonymous visitors and clients.</summary>
    public bool IsPubliclyVisible => Status == PropertyStatus.Available;

    public static Property Create(Guid ownerId, PropertyDetails details, DateTime utcNow)
    {
        if (ownerId == Guid.Empty)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                [nameof(OwnerId)] = ["A property must have an owner."],
            });
        }

        var property = new Property
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            CreatedAt = utcNow,
        };
        property.Apply(details, utcNow);
        return property;
    }

    public bool IsOwnedBy(Guid userId) => OwnerId == userId;

    public void Update(PropertyDetails details, DateTime utcNow) => Apply(details, utcNow);

    public int PhotoCount => _images.Count + _imageUrls.Count;

    public bool CanAddImage => PhotoCount < MaxImages;

    public void AddImage(PropertyImage image, DateTime utcNow)
    {
        if (!CanAddImage)
        {
            throw new DomainValidationException(new Dictionary<string, string[]>
            {
                [nameof(Images)] = [$"A property can have at most {MaxImages} images."],
            });
        }

        _images.Add(image);
        UpdatedAt = utcNow;
    }

    public PropertyImage? RemoveImage(Guid imageId, DateTime utcNow)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId);
        if (image is not null)
        {
            _images.Remove(image);
            UpdatedAt = utcNow;
        }

        return image;
    }

    public int NextImageSortOrder => _images.Count == 0 ? 0 : _images.Max(i => i.SortOrder) + 1;

    /// <summary>
    /// Single source of the property rules. Used by the entity itself and by the
    /// application layer to report every problem at once instead of failing on the first.
    /// </summary>
    /// <param name="uploadedImageCount">Uploaded photos already attached, which share the image limit.</param>
    public static IReadOnlyDictionary<string, string[]> Validate(PropertyDetails details, int uploadedImageCount = 0)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list))
            {
                errors[field] = list = [];
            }

            list.Add(message);
        }

        RequireText(details.Title, nameof(details.Title), TitleMaxLength, Add);
        RequireText(details.Description, nameof(details.Description), DescriptionMaxLength, Add);
        RequireText(details.Country, nameof(details.Country), LocationMaxLength, Add);
        RequireText(details.Department, nameof(details.Department), LocationMaxLength, Add);
        RequireText(details.City, nameof(details.City), LocationMaxLength, Add);
        RequireText(details.Address, nameof(details.Address), AddressMaxLength, Add);

        RequireDefined(details.PropertyType, nameof(details.PropertyType), Add);
        RequireDefined(details.TransactionType, nameof(details.TransactionType), Add);
        RequireDefined(details.Currency, nameof(details.Currency), Add);
        RequireDefined(details.Status, nameof(details.Status), Add);

        if (details.Price <= 0 || details.Price > MaxPrice)
        {
            Add(nameof(details.Price), "Price must be greater than 0.");
        }

        if (details.Bedrooms is < 0 or > MaxRooms)
        {
            Add(nameof(details.Bedrooms), $"Bedrooms must be between 0 and {MaxRooms}.");
        }

        if (details.Bathrooms is < 0 or > MaxRooms)
        {
            Add(nameof(details.Bathrooms), $"Bathrooms must be between 0 and {MaxRooms}.");
        }

        if (details.Area is <= 0)
        {
            Add(nameof(details.Area), "Area must be greater than 0.");
        }

        var images = details.ImageUrls ?? [];
        if (images.Count + uploadedImageCount > MaxImages)
        {
            Add(nameof(details.ImageUrls), $"A property can have at most {MaxImages} images.");
        }

        if (images.Any(url => !IsValidImageUrl(url)))
        {
            Add(nameof(details.ImageUrls), "Each image must be an absolute http(s) URL.");
        }

        return errors.ToDictionary(e => e.Key, e => e.Value.ToArray());
    }

    private void Apply(PropertyDetails details, DateTime utcNow)
    {
        var errors = Validate(details, _images.Count);
        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        Title = details.Title.Trim();
        Description = details.Description.Trim();
        PropertyType = details.PropertyType;
        TransactionType = details.TransactionType;
        Price = details.Price;
        Currency = details.Currency;
        Country = details.Country.Trim();
        Department = details.Department.Trim();
        City = details.City.Trim();
        Address = details.Address.Trim();
        Bedrooms = details.Bedrooms;
        Bathrooms = details.Bathrooms;
        Area = details.Area;
        Status = details.Status;
        _imageUrls = (details.ImageUrls ?? []).Select(url => url.Trim()).ToList();
        SearchKey = SearchText.Join(Title, Description);
        LocationKey = SearchText.Join(City, Department, Address);
        DepartmentKey = SearchText.Normalize(Department);
        UpdatedAt = utcNow;
    }

    private static void RequireText(string? value, string field, int maxLength, Action<string, string> add)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            add(field, $"{field} is required.");
        }
        else if (value.Trim().Length > maxLength)
        {
            add(field, $"{field} must be at most {maxLength} characters.");
        }
    }

    private static void RequireDefined<TEnum>(TEnum value, string field, Action<string, string> add)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            add(field, $"{field} has an invalid value.");
        }
    }

    private static bool IsValidImageUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && url.Trim().Length <= ImageUrlMaxLength
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
