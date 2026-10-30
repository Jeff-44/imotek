using Imotek.Application.Common;
using Imotek.Application.Properties;
using Imotek.Domain.Properties;
using Imotek.Infrastructure.Images;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Application;

/// <summary>Authorization and visibility rules of <see cref="PropertyService"/>, without a database.</summary>
public class PropertyServiceTests
{
    private static readonly Guid AgentA = Guid.NewGuid();
    private static readonly Guid AgentB = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();

    private readonly InMemoryPropertyRepository _repository = new();
    private readonly FixedTimeProvider _time = new(PropertyTestData.Now);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private InMemoryFileStorage Storage { get; } = new();

    private PropertyService ServiceFor(ICurrentUser user) => new(_repository, user, _time, Storage, new ImageProcessor());

    private Property Seed(Guid ownerId, PropertyStatus status = PropertyStatus.Available)
    {
        var property = PropertyTestData.Property(ownerId, PropertyTestData.Details(status: status));
        _repository.Items.Add(property);
        return property;
    }

    // ---- Create -------------------------------------------------------------

    [Fact]
    public async Task Agent_creates_property_owned_by_the_authenticated_user()
    {
        var result = await ServiceFor(FakeCurrentUser.Agent(AgentA)).CreateAsync(PropertyTestData.Request(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(AgentA, result.Value.OwnerId);
        var stored = Assert.Single(_repository.Items);
        Assert.Equal(AgentA, stored.OwnerId);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Create_defaults_to_draft_and_Haiti()
    {
        var request = PropertyTestData.Request(status: null) with { Country = null };

        var result = await ServiceFor(FakeCurrentUser.Agent(AgentA)).CreateAsync(request, _ct);

        Assert.Equal(PropertyStatus.Draft, result.Value.Status);
        Assert.Equal(PropertyService.DefaultCountry, result.Value.Country);
    }

    [Fact]
    public async Task Create_with_invalid_data_returns_every_error_and_saves_nothing()
    {
        var request = PropertyTestData.Request(title: "", price: -1, propertyType: null);

        var result = await ServiceFor(FakeCurrentUser.Agent(AgentA)).CreateAsync(request, _ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains(nameof(PropertyRequest.Title), result.Error.ValidationErrors!.Keys);
        Assert.Contains(nameof(PropertyRequest.Price), result.Error.ValidationErrors.Keys);
        Assert.Contains(nameof(PropertyRequest.PropertyType), result.Error.ValidationErrors.Keys);
        Assert.Empty(_repository.Items);
    }

    [Fact]
    public async Task Client_cannot_create()
    {
        var result = await ServiceFor(FakeCurrentUser.Client(ClientId)).CreateAsync(PropertyTestData.Request(), _ct);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Empty(_repository.Items);
    }

    [Fact]
    public async Task Admin_cannot_create_because_only_agents_publish()
    {
        var result = await ServiceFor(FakeCurrentUser.Admin(AdminId)).CreateAsync(PropertyTestData.Request(), _ct);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
    }

    [Fact]
    public async Task Anonymous_cannot_create()
    {
        var result = await ServiceFor(FakeCurrentUser.Anonymous).CreateAsync(PropertyTestData.Request(), _ct);

        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
    }

    // ---- Update -------------------------------------------------------------

    [Fact]
    public async Task Agent_updates_own_property()
    {
        var property = Seed(AgentA, PropertyStatus.Draft);
        _time.UtcNow = PropertyTestData.Now.AddHours(1);

        var result = await ServiceFor(FakeCurrentUser.Agent(AgentA))
            .UpdateAsync(property.Id, PropertyTestData.Request(title: "Nouveau titre", price: 900m), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Nouveau titre", property.Title);
        Assert.Equal(900m, property.Price);
        Assert.Equal(AgentA, property.OwnerId);
        Assert.Equal(PropertyTestData.Now.AddHours(1), property.UpdatedAt);
    }

    [Fact]
    public async Task Agent_cannot_update_another_agents_visible_property()
    {
        var property = Seed(AgentA);

        var result = await ServiceFor(FakeCurrentUser.Agent(AgentB))
            .UpdateAsync(property.Id, PropertyTestData.Request(title: "Piraté"), _ct);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.NotEqual("Piraté", property.Title);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Theory]
    [InlineData(PropertyStatus.Draft)]
    [InlineData(PropertyStatus.Rented)]
    [InlineData(PropertyStatus.Sold)]
    public async Task Another_agents_hidden_property_is_reported_as_not_found(PropertyStatus status)
    {
        var property = Seed(AgentA, status);

        var result = await ServiceFor(FakeCurrentUser.Agent(AgentB)).UpdateAsync(property.Id, PropertyTestData.Request(), _ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Update_of_missing_property_is_not_found()
    {
        var result = await ServiceFor(FakeCurrentUser.Agent(AgentA)).UpdateAsync(Guid.NewGuid(), PropertyTestData.Request(), _ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Update_with_invalid_data_leaves_property_unchanged()
    {
        var property = Seed(AgentA);

        var result = await ServiceFor(FakeCurrentUser.Agent(AgentA)).UpdateAsync(property.Id, PropertyTestData.Request(price: 0), _ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal(1500m, property.Price);
    }

    // ---- Delete -------------------------------------------------------------

    [Fact]
    public async Task Agent_deletes_own_property()
    {
        var property = Seed(AgentA);

        var result = await ServiceFor(FakeCurrentUser.Agent(AgentA)).DeleteAsync(property.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(_repository.Items);
    }

    [Fact]
    public async Task Agent_cannot_delete_another_agents_property()
    {
        var property = Seed(AgentA);

        var result = await ServiceFor(FakeCurrentUser.Agent(AgentB)).DeleteAsync(property.Id, _ct);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Contains(property, _repository.Items);
    }

    // ---- Admin --------------------------------------------------------------

    [Theory]
    [InlineData(PropertyStatus.Available)]
    [InlineData(PropertyStatus.Draft)]
    public async Task Admin_updates_any_property_without_taking_ownership(PropertyStatus status)
    {
        var property = Seed(AgentA, status);

        var result = await ServiceFor(FakeCurrentUser.Admin(AdminId))
            .UpdateAsync(property.Id, PropertyTestData.Request(status: PropertyStatus.Sold), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(PropertyStatus.Sold, property.Status);
        Assert.Equal(AgentA, property.OwnerId);
    }

    [Fact]
    public async Task Admin_deletes_any_property()
    {
        var property = Seed(AgentA, PropertyStatus.Draft);

        var result = await ServiceFor(FakeCurrentUser.Admin(AdminId)).DeleteAsync(property.Id, _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(_repository.Items);
    }

    [Fact]
    public async Task Admin_lists_every_property_in_every_status()
    {
        Seed(AgentA, PropertyStatus.Draft);
        Seed(AgentA, PropertyStatus.Available);
        Seed(AgentB, PropertyStatus.Sold);
        Seed(AgentB, PropertyStatus.Rented);

        var result = await ServiceFor(FakeCurrentUser.Admin(AdminId)).SearchAllAsync(new PropertySearchRequest(), _ct);

        Assert.Equal(4, result.Value.TotalCount);
    }

    [Fact]
    public async Task Admin_can_filter_by_status()
    {
        Seed(AgentA, PropertyStatus.Draft);
        Seed(AgentB, PropertyStatus.Available);

        var result = await ServiceFor(FakeCurrentUser.Admin(AdminId))
            .SearchAllAsync(new PropertySearchRequest { Status = PropertyStatus.Draft }, _ct);

        Assert.Equal(PropertyStatus.Draft, Assert.Single(result.Value.Items).Status);
    }

    [Fact]
    public async Task Non_admins_cannot_list_all_properties()
    {
        Assert.Equal(ErrorType.Forbidden, (await ServiceFor(FakeCurrentUser.Agent(AgentA)).SearchAllAsync(new(), _ct)).Error!.Type);
        Assert.Equal(ErrorType.Forbidden, (await ServiceFor(FakeCurrentUser.Client(ClientId)).SearchAllAsync(new(), _ct)).Error!.Type);
    }

    // ---- Client -------------------------------------------------------------

    [Fact]
    public async Task Client_cannot_update_or_delete()
    {
        var property = Seed(AgentA);
        var service = ServiceFor(FakeCurrentUser.Client(ClientId));

        var update = await service.UpdateAsync(property.Id, PropertyTestData.Request(title: "x"), _ct);
        var delete = await service.DeleteAsync(property.Id, _ct);

        Assert.Equal(ErrorType.Forbidden, update.Error!.Type);
        Assert.Equal(ErrorType.Forbidden, delete.Error!.Type);
        Assert.Contains(property, _repository.Items);
        Assert.NotEqual("x", property.Title);
    }

    [Fact]
    public async Task Client_has_no_my_properties_list()
    {
        var result = await ServiceFor(FakeCurrentUser.Client(ClientId)).SearchMineAsync(new PropertySearchRequest(), _ct);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
    }

    // ---- Visibility ---------------------------------------------------------

    [Theory]
    [InlineData(PropertyStatus.Draft)]
    [InlineData(PropertyStatus.Rented)]
    [InlineData(PropertyStatus.Sold)]
    public async Task Hidden_property_details_are_not_found_for_visitors_clients_and_other_agents(PropertyStatus status)
    {
        var property = Seed(AgentA, status);

        foreach (var user in new[] { FakeCurrentUser.Anonymous, FakeCurrentUser.Client(ClientId), FakeCurrentUser.Agent(AgentB) })
        {
            var result = await ServiceFor(user).GetByIdAsync(property.Id, _ct);
            Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        }
    }

    [Fact]
    public async Task Hidden_property_details_are_visible_to_owner_and_admin()
    {
        var property = Seed(AgentA, PropertyStatus.Draft);

        Assert.True((await ServiceFor(FakeCurrentUser.Agent(AgentA)).GetByIdAsync(property.Id, _ct)).IsSuccess);
        Assert.True((await ServiceFor(FakeCurrentUser.Admin(AdminId)).GetByIdAsync(property.Id, _ct)).IsSuccess);
    }

    [Fact]
    public async Task Available_property_details_are_public_with_price()
    {
        var property = Seed(AgentA);

        var result = await ServiceFor(FakeCurrentUser.Anonymous).GetByIdAsync(property.Id, _ct);

        Assert.Equal(1500m, result.Value.Price);
        Assert.Equal(Currency.USD, result.Value.Currency);
    }

    [Fact]
    public async Task Agent_sees_all_own_properties_with_their_status_and_nobody_elses()
    {
        Seed(AgentA, PropertyStatus.Draft);
        Seed(AgentA, PropertyStatus.Available);
        Seed(AgentA, PropertyStatus.Sold);
        Seed(AgentB, PropertyStatus.Available);

        var result = await ServiceFor(FakeCurrentUser.Agent(AgentA)).SearchMineAsync(new PropertySearchRequest(), _ct);

        Assert.Equal(3, result.Value.TotalCount);
        Assert.All(result.Value.Items, p => Assert.Equal(AgentA, p.OwnerId));
        Assert.Equivalent(
            new[] { PropertyStatus.Draft, PropertyStatus.Available, PropertyStatus.Sold },
            result.Value.Items.Select(p => p.Status).ToArray());
    }
}
