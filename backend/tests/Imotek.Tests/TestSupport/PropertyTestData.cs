using Imotek.Application.Properties;
using Imotek.Domain.Properties;

namespace Imotek.Tests.TestSupport;

internal static class PropertyTestData
{
    public static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    public static PropertyDetails Details(
        string title = "Maison à louer",
        PropertyType propertyType = PropertyType.House,
        TransactionType transactionType = TransactionType.Rent,
        decimal price = 1500m,
        Currency currency = Currency.USD,
        string department = "Ouest",
        string city = "Pétion-Ville",
        PropertyStatus status = PropertyStatus.Available,
        IReadOnlyList<string>? imageUrls = null,
        int? bedrooms = 3,
        decimal? area = 120m) =>
        new(
            Title: title,
            Description: "Description du bien",
            PropertyType: propertyType,
            TransactionType: transactionType,
            Price: price,
            Currency: currency,
            Country: "Haïti",
            Department: department,
            City: city,
            Address: "12, rue Exemple",
            Bedrooms: bedrooms,
            Bathrooms: 2,
            Area: area,
            Status: status,
            ImageUrls: imageUrls ?? ["https://images.example.com/1.jpg"]);

    public static PropertyRequest Request(
        string title = "Maison à louer",
        PropertyType? propertyType = PropertyType.House,
        TransactionType? transactionType = TransactionType.Rent,
        decimal? price = 1500m,
        Currency? currency = Currency.USD,
        string department = "Ouest",
        string city = "Pétion-Ville",
        PropertyStatus? status = PropertyStatus.Available) =>
        new()
        {
            Title = title,
            Description = "Description du bien",
            PropertyType = propertyType,
            TransactionType = transactionType,
            Price = price,
            Currency = currency,
            Department = department,
            City = city,
            Address = "12, rue Exemple",
            Bedrooms = 3,
            Bathrooms = 2,
            Area = 120m,
            Status = status,
            ImageUrls = ["https://images.example.com/1.jpg"],
        };

    public static Property Property(Guid ownerId, PropertyDetails? details = null, DateTime? createdAt = null) =>
        global::Imotek.Domain.Properties.Property.Create(ownerId, details ?? Details(), createdAt ?? Now);
}
