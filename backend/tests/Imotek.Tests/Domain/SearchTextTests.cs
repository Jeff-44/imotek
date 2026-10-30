using Imotek.Domain.Common;
using Imotek.Domain.Properties;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Domain;

public class SearchTextTests
{
    [Theory]
    [InlineData("Pétion-Ville", "petion-ville")]
    [InlineData("CAP-HAÏTIEN", "cap-haitien")]
    [InlineData("Grand’Anse", "grand'anse")]
    [InlineData("  Côte   des  Arcadins ", "cote des arcadins")]
    [InlineData("Cœur", "coeur")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_removes_case_accents_and_extra_spaces(string? input, string expected)
    {
        Assert.Equal(expected, SearchText.Normalize(input));
    }

    [Fact]
    public void Property_keeps_normalized_search_keys_in_sync()
    {
        var property = PropertyTestData.Property(
            Guid.NewGuid(), PropertyTestData.Details(title: "Villa Élégante", city: "Pétion-Ville", department: "Ouest"));

        Assert.Contains("villa elegante", property.SearchKey);
        Assert.Contains("petion-ville", property.LocationKey);
        Assert.Equal("ouest", property.DepartmentKey);

        property.Update(PropertyTestData.Details(title: "Maison", city: "Jérémie", department: "Grand'Anse"), PropertyTestData.Now);

        Assert.DoesNotContain("villa", property.SearchKey);
        Assert.Contains("jeremie", property.LocationKey);
        Assert.Equal("grand'anse", property.DepartmentKey);
    }

    [Fact]
    public void Uploaded_images_share_the_image_limit_with_links()
    {
        var links = Enumerable.Range(0, Property.MaxImages - 1).Select(i => $"https://example.com/{i}.jpg").ToList();
        var property = PropertyTestData.Property(Guid.NewGuid(), PropertyTestData.Details(imageUrls: links));

        property.AddImage(PropertyImage.Create(Guid.NewGuid(), "k1", "image/jpeg", 10, 0, PropertyTestData.Now), PropertyTestData.Now);

        Assert.False(property.CanAddImage);
        Assert.Throws<global::Imotek.Domain.Common.DomainValidationException>(
            () => property.AddImage(PropertyImage.Create(Guid.NewGuid(), "k2", "image/jpeg", 10, 1, PropertyTestData.Now), PropertyTestData.Now));
        Assert.Throws<global::Imotek.Domain.Common.DomainValidationException>(
            () => property.Update(PropertyTestData.Details(imageUrls: [.. links, "https://example.com/extra.jpg"]), PropertyTestData.Now));
    }
}
