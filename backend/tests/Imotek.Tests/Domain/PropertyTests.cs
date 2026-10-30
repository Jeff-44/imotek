using Imotek.Domain.Common;
using Imotek.Domain.Properties;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Domain;

public class PropertyTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    public void Create_sets_owner_timestamps_and_trimmed_values()
    {
        var details = PropertyTestData.Details(title: "  Villa  ", city: " Jacmel ");

        var property = Property.Create(OwnerId, details, PropertyTestData.Now);

        Assert.NotEqual(Guid.Empty, property.Id);
        Assert.Equal(OwnerId, property.OwnerId);
        Assert.Equal("Villa", property.Title);
        Assert.Equal("Jacmel", property.City);
        Assert.Equal(PropertyTestData.Now, property.CreatedAt);
        Assert.Equal(PropertyTestData.Now, property.UpdatedAt);
    }

    [Fact]
    public void Create_without_owner_is_rejected()
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => Property.Create(Guid.Empty, PropertyTestData.Details(), PropertyTestData.Now));

        Assert.Contains(nameof(Property.OwnerId), ex.Errors.Keys);
    }

    [Fact]
    public void Create_with_invalid_details_throws_and_reports_the_field()
    {
        var ex = Assert.Throws<DomainValidationException>(
            () => Property.Create(OwnerId, PropertyTestData.Details(price: 0), PropertyTestData.Now));

        Assert.Equal([nameof(PropertyDetails.Price)], ex.Errors.Keys);
    }

    [Fact]
    public void Update_changes_values_and_UpdatedAt_but_keeps_owner_and_CreatedAt()
    {
        var property = PropertyTestData.Property(OwnerId);
        var later = PropertyTestData.Now.AddDays(2);

        property.Update(PropertyTestData.Details(price: 2000m, status: PropertyStatus.Rented), later);

        Assert.Equal(2000m, property.Price);
        Assert.Equal(PropertyStatus.Rented, property.Status);
        Assert.Equal(OwnerId, property.OwnerId);
        Assert.Equal(PropertyTestData.Now, property.CreatedAt);
        Assert.Equal(later, property.UpdatedAt);
    }

    [Theory]
    [InlineData(PropertyStatus.Available, true)]
    [InlineData(PropertyStatus.Draft, false)]
    [InlineData(PropertyStatus.Rented, false)]
    [InlineData(PropertyStatus.Sold, false)]
    public void Only_available_properties_are_publicly_visible(PropertyStatus status, bool expected)
    {
        var property = PropertyTestData.Property(OwnerId, PropertyTestData.Details(status: status));

        Assert.Equal(expected, property.IsPubliclyVisible);
    }

    [Fact]
    public void Validate_accepts_valid_details()
    {
        Assert.Empty(Property.Validate(PropertyTestData.Details()));
    }

    public static TheoryData<PropertyDetails, string> InvalidDetails => new()
    {
        { PropertyTestData.Details(title: " "), nameof(PropertyDetails.Title) },
        { PropertyTestData.Details(title: new string('a', Property.TitleMaxLength + 1)), nameof(PropertyDetails.Title) },
        { PropertyTestData.Details(price: 0), nameof(PropertyDetails.Price) },
        { PropertyTestData.Details(price: -10), nameof(PropertyDetails.Price) },
        { PropertyTestData.Details(city: ""), nameof(PropertyDetails.City) },
        { PropertyTestData.Details(department: ""), nameof(PropertyDetails.Department) },
        { PropertyTestData.Details(bedrooms: -1), nameof(PropertyDetails.Bedrooms) },
        { PropertyTestData.Details(area: 0), nameof(PropertyDetails.Area) },
        { PropertyTestData.Details(propertyType: (PropertyType)99), nameof(PropertyDetails.PropertyType) },
        { PropertyTestData.Details(imageUrls: ["ftp://example.com/a.jpg"]), nameof(PropertyDetails.ImageUrls) },
        { PropertyTestData.Details(imageUrls: ["not a url"]), nameof(PropertyDetails.ImageUrls) },
        {
            PropertyTestData.Details(imageUrls: Enumerable.Range(0, Property.MaxImages + 1).Select(i => $"https://example.com/{i}.jpg").ToList()),
            nameof(PropertyDetails.ImageUrls)
        },
    };

    [Theory]
    [MemberData(nameof(InvalidDetails))]
    public void Validate_reports_the_invalid_field(PropertyDetails details, string field)
    {
        var errors = Property.Validate(details);

        Assert.Equal([field], errors.Keys);
    }
}
