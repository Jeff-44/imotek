using Imotek.Application.Common;
using Imotek.Application.Properties;
using Imotek.Domain.Properties;
using Imotek.Infrastructure.Images;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Application;

/// <summary>Accent-insensitive search and the no-currency-mixing rule.</summary>
public class SearchRulesTests
{
    private readonly InMemoryPropertyRepository _repository = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private PropertyService Service =>
        new(_repository, FakeCurrentUser.Anonymous, new FixedTimeProvider(PropertyTestData.Now), new InMemoryFileStorage(), new ImageProcessor());

    private Property Seed(PropertyDetails details)
    {
        var property = PropertyTestData.Property(Guid.NewGuid(), details);
        _repository.Items.Add(property);
        return property;
    }

    [Theory]
    [InlineData("petion")]
    [InlineData("PÉTION")]
    [InlineData("Pétion-Ville")]
    [InlineData("  petion-ville ")]
    public async Task Location_search_ignores_accents_and_case(string term)
    {
        var property = Seed(PropertyTestData.Details(city: "Pétion-Ville"));

        var result = await Service.SearchPublicAsync(new PropertySearchRequest { Location = term }, _ct);

        Assert.Equal(property.Id, Assert.Single(result.Value.Items).Id);
    }

    [Fact]
    public async Task Text_search_ignores_accents()
    {
        var property = Seed(PropertyTestData.Details(title: "Maison élégante à Kenscoff"));

        var result = await Service.SearchPublicAsync(new PropertySearchRequest { Search = "ELEGANTE a kenscoff" }, _ct);

        Assert.Equal(property.Id, Assert.Single(result.Value.Items).Id);
    }

    [Theory]
    [InlineData("Grand'Anse")]
    [InlineData("grand’anse")]
    [InlineData("GRAND'ANSE")]
    public async Task Department_filter_matches_apostrophe_variants(string department)
    {
        var property = Seed(PropertyTestData.Details(department: "Grand'Anse"));
        Seed(PropertyTestData.Details(department: "Nippes"));

        var result = await Service.SearchPublicAsync(new PropertySearchRequest { Department = department }, _ct);

        Assert.Equal(property.Id, Assert.Single(result.Value.Items).Id);
    }

    [Theory]
    [InlineData(100, null)]
    [InlineData(null, 5000)]
    [InlineData(100, 5000)]
    public async Task Price_range_without_currency_is_rejected(int? minPrice, int? maxPrice)
    {
        var result = await Service.SearchPublicAsync(new PropertySearchRequest { MinPrice = minPrice, MaxPrice = maxPrice }, _ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains(nameof(PropertySearchRequest.Currency), result.Error.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Price_range_never_mixes_HTG_and_USD()
    {
        // 1 000 HTG and 1 000 USD are very different amounts: only the requested currency matches.
        var htg = Seed(PropertyTestData.Details(price: 1000m, currency: Currency.HTG));
        var usd = Seed(PropertyTestData.Details(price: 1000m, currency: Currency.USD));

        var inHtg = await Service.SearchPublicAsync(new() { MinPrice = 500, MaxPrice = 1500, Currency = Currency.HTG }, _ct);
        var inUsd = await Service.SearchPublicAsync(new() { MinPrice = 500, MaxPrice = 1500, Currency = Currency.USD }, _ct);

        Assert.Equal(htg.Id, Assert.Single(inHtg.Value.Items).Id);
        Assert.Equal(usd.Id, Assert.Single(inUsd.Value.Items).Id);
    }

    [Fact]
    public async Task Currency_alone_filters_without_a_price_range()
    {
        Seed(PropertyTestData.Details(currency: Currency.HTG));
        var usd = Seed(PropertyTestData.Details(currency: Currency.USD));

        var result = await Service.SearchPublicAsync(new() { Currency = Currency.USD }, _ct);

        Assert.Equal(usd.Id, Assert.Single(result.Value.Items).Id);
    }
}
