using Imotek.Application.Common;
using Imotek.Application.Properties;
using Imotek.Domain.Properties;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Application;

/// <summary>Public search rules and filters. The same LINQ filter is also run against PostgreSQL in the API tests.</summary>
public class PropertySearchTests
{
    private static readonly Guid Owner = Guid.NewGuid();

    private readonly InMemoryPropertyRepository _repository = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private PropertyService PublicService => new(_repository, FakeCurrentUser.Anonymous, new FixedTimeProvider(PropertyTestData.Now));

    private Property Seed(PropertyDetails details, int minutesAgo = 0)
    {
        var property = PropertyTestData.Property(Owner, details, PropertyTestData.Now.AddMinutes(-minutesAgo));
        _repository.Items.Add(property);
        return property;
    }

    private async Task<IReadOnlyList<PropertyResponse>> Search(PropertySearchRequest request)
    {
        var result = await PublicService.SearchPublicAsync(request, _ct);
        Assert.True(result.IsSuccess);
        return result.Value.Items;
    }

    [Fact]
    public async Task Public_search_returns_only_available_properties()
    {
        var available = Seed(PropertyTestData.Details(status: PropertyStatus.Available));
        Seed(PropertyTestData.Details(status: PropertyStatus.Draft));
        Seed(PropertyTestData.Details(status: PropertyStatus.Rented));
        Seed(PropertyTestData.Details(status: PropertyStatus.Sold));

        var items = await Search(new PropertySearchRequest());

        Assert.Equal(available.Id, Assert.Single(items).Id);
    }

    [Theory]
    [InlineData(PropertyStatus.Draft)]
    [InlineData(PropertyStatus.Rented)]
    [InlineData(PropertyStatus.Sold)]
    public async Task Public_search_ignores_a_requested_hidden_status(PropertyStatus status)
    {
        Seed(PropertyTestData.Details(status: status));

        Assert.Empty(await Search(new PropertySearchRequest { Status = status }));
    }

    [Fact]
    public async Task Filters_by_location_in_city_department_or_address_case_insensitively()
    {
        var petionVille = Seed(PropertyTestData.Details(city: "Pétion-Ville", department: "Ouest"));
        var capHaitien = Seed(PropertyTestData.Details(city: "Cap-Haïtien", department: "Nord"));

        Assert.Equal(petionVille.Id, Assert.Single(await Search(new() { Location = "pétion" })).Id);
        Assert.Equal(capHaitien.Id, Assert.Single(await Search(new() { Location = "NORD" })).Id);
    }

    [Fact]
    public async Task Filters_by_exact_department()
    {
        Seed(PropertyTestData.Details(department: "Nord"));
        var nordEst = Seed(PropertyTestData.Details(department: "Nord-Est"));

        Assert.Equal(nordEst.Id, Assert.Single(await Search(new() { Department = "nord-est" })).Id);
    }

    [Fact]
    public async Task Filters_by_property_type()
    {
        Seed(PropertyTestData.Details(propertyType: PropertyType.House));
        var land = Seed(PropertyTestData.Details(propertyType: PropertyType.Land));

        Assert.Equal(land.Id, Assert.Single(await Search(new() { PropertyType = PropertyType.Land })).Id);
    }

    [Fact]
    public async Task Filters_by_rent_or_sale()
    {
        var rent = Seed(PropertyTestData.Details(transactionType: TransactionType.Rent));
        var sale = Seed(PropertyTestData.Details(transactionType: TransactionType.Sale));

        Assert.Equal(rent.Id, Assert.Single(await Search(new() { TransactionType = TransactionType.Rent })).Id);
        Assert.Equal(sale.Id, Assert.Single(await Search(new() { TransactionType = TransactionType.Sale })).Id);
    }

    [Fact]
    public async Task Filters_by_inclusive_price_range_and_currency()
    {
        Seed(PropertyTestData.Details(price: 500m));
        var mid = Seed(PropertyTestData.Details(price: 1000m));
        var top = Seed(PropertyTestData.Details(price: 2000m));
        Seed(PropertyTestData.Details(price: 1000m, currency: Currency.HTG));

        var items = await Search(new() { MinPrice = 1000m, MaxPrice = 2000m, Currency = Currency.USD });

        Assert.Equivalent(new[] { mid.Id, top.Id }, items.Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task Filters_by_text_in_title_or_description()
    {
        Seed(PropertyTestData.Details(title: "Appartement meublé"));
        var villa = Seed(PropertyTestData.Details(title: "Villa avec piscine"));

        Assert.Equal(villa.Id, Assert.Single(await Search(new() { Search = "PISCINE" })).Id);
    }

    [Fact]
    public async Task Combines_filters()
    {
        Seed(PropertyTestData.Details(city: "Jacmel", transactionType: TransactionType.Sale));
        var match = Seed(PropertyTestData.Details(city: "Jacmel", transactionType: TransactionType.Rent, price: 800m));
        Seed(PropertyTestData.Details(city: "Jacmel", transactionType: TransactionType.Rent, price: 5000m));

        var items = await Search(new() { Location = "jacmel", TransactionType = TransactionType.Rent, MaxPrice = 1000m });

        Assert.Equal(match.Id, Assert.Single(items).Id);
    }

    [Fact]
    public async Task Paginates_newest_first()
    {
        var oldest = Seed(PropertyTestData.Details(), minutesAgo: 30);
        var middle = Seed(PropertyTestData.Details(), minutesAgo: 20);
        var newest = Seed(PropertyTestData.Details(), minutesAgo: 10);

        var page1 = await PublicService.SearchPublicAsync(new() { PageSize = 2, Page = 1 }, _ct);
        var page2 = await PublicService.SearchPublicAsync(new() { PageSize = 2, Page = 2 }, _ct);

        Assert.Equal([newest.Id, middle.Id], page1.Value.Items.Select(p => p.Id));
        Assert.Equal([oldest.Id], page2.Value.Items.Select(p => p.Id));
        Assert.Equal(3, page1.Value.TotalCount);
        Assert.Equal(2, page1.Value.TotalPages);
    }

    [Theory]
    [InlineData(0, 12, null, null, nameof(PropertySearchRequest.Page))]
    [InlineData(1, 0, null, null, nameof(PropertySearchRequest.PageSize))]
    [InlineData(1, PropertyService.MaxPageSize + 1, null, null, nameof(PropertySearchRequest.PageSize))]
    [InlineData(1, 12, -1, null, nameof(PropertySearchRequest.MinPrice))]
    [InlineData(1, 12, 2000, 1000, nameof(PropertySearchRequest.MaxPrice))]
    public async Task Rejects_invalid_search_parameters(int page, int pageSize, int? minPrice, int? maxPrice, string field)
    {
        var result = await PublicService.SearchPublicAsync(
            new() { Page = page, PageSize = pageSize, MinPrice = minPrice, MaxPrice = maxPrice }, _ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains(field, result.Error.ValidationErrors!.Keys);
    }
}
