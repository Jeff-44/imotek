using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Imotek.Application.Common;
using Imotek.Application.Properties;
using Imotek.Domain.Properties;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Api;

/// <summary>End to end over HTTP: authentication, authorization and SQL-translated filters on PostgreSQL.</summary>
[Collection(PostgresApiCollection.Name)]
public class PropertiesApiTests
{
    private readonly PostgresApiFixture _api;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public PropertiesApiTests(PostgresApiFixture api)
    {
        _api = api;
        _api.SkipIfUnavailable();
    }

    private static object Body(
        string title = "Maison test",
        string city = "Pétion-Ville",
        string transactionType = "Rent",
        string propertyType = "House",
        decimal price = 1500m,
        string status = "Available") => new
        {
            title,
            description = "Belle maison",
            propertyType,
            transactionType,
            price,
            currency = "USD",
            department = "Ouest",
            city,
            address = "12, rue Exemple",
            bedrooms = 3,
            status,
            imageUrls = new[] { "https://images.example.com/1.jpg" },
        };

    private async Task<PropertyResponse> CreateAsync(HttpClient agent, object body)
    {
        var response = await agent.PostAsJsonAsync("/api/properties", body, _ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PropertyResponse>(PostgresApiFixture.Json, _ct))!;
    }

    private async Task<PagedResult<PropertyResponse>> GetPageAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, _ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResult<PropertyResponse>>(PostgresApiFixture.Json, _ct))!;
    }

    [Fact]
    public async Task Agent_creates_reads_updates_and_deletes_own_property()
    {
        var (agent, agentId) = await _api.NewAgentAsync();

        var created = await CreateAsync(agent, Body(status: "Draft"));
        Assert.Equal(agentId, created.OwnerId);
        Assert.Equal(PropertyStatus.Draft, created.Status);

        var mine = await GetPageAsync(agent, "/api/properties/my-properties");
        Assert.Contains(mine.Items, p => p.Id == created.Id && p.Status == PropertyStatus.Draft);

        var update = await agent.PutAsJsonAsync($"/api/properties/{created.Id}", Body(title: "Titre modifié", status: "Available"), _ct);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<PropertyResponse>(PostgresApiFixture.Json, _ct);
        Assert.Equal("Titre modifié", updated!.Title);

        var publicDetail = await _api.Anonymous().GetAsync($"/api/properties/{created.Id}", _ct);
        Assert.Equal(HttpStatusCode.OK, publicDetail.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await agent.DeleteAsync($"/api/properties/{created.Id}", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await agent.GetAsync($"/api/properties/{created.Id}", _ct)).StatusCode);
    }

    [Fact]
    public async Task OwnerId_in_the_request_body_is_ignored()
    {
        var (agent, agentId) = await _api.NewAgentAsync();
        var (_, victimId) = await _api.NewAgentAsync();

        var response = await agent.PostAsJsonAsync(
            "/api/properties",
            new Dictionary<string, object?>
            {
                ["ownerId"] = victimId,
                ["title"] = "Usurpation",
                ["description"] = "x",
                ["propertyType"] = "Land",
                ["transactionType"] = "Sale",
                ["price"] = 10,
                ["currency"] = "HTG",
                ["department"] = "Sud",
                ["city"] = "Les Cayes",
                ["address"] = "Route 1",
            },
            _ct);

        var created = await response.Content.ReadFromJsonAsync<PropertyResponse>(PostgresApiFixture.Json, _ct);
        Assert.Equal(agentId, created!.OwnerId);
    }

    [Fact]
    public async Task Agent_cannot_update_or_delete_another_agents_property()
    {
        var (owner, _) = await _api.NewAgentAsync();
        var (intruder, _) = await _api.NewAgentAsync();
        var visible = await CreateAsync(owner, Body(status: "Available"));
        var draft = await CreateAsync(owner, Body(status: "Draft"));

        Assert.Equal(HttpStatusCode.Forbidden, (await intruder.PutAsJsonAsync($"/api/properties/{visible.Id}", Body(title: "x"), _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await intruder.DeleteAsync($"/api/properties/{visible.Id}", _ct)).StatusCode);

        // A draft is hidden from other agents, so its existence is not revealed.
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PutAsJsonAsync($"/api/properties/{draft.Id}", Body(), _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync($"/api/properties/{draft.Id}", _ct)).StatusCode);

        var stillThere = await owner.GetFromJsonAsync<PropertyResponse>($"/api/properties/{visible.Id}", PostgresApiFixture.Json, _ct);
        Assert.Equal("Maison test", stillThere!.Title);
    }

    [Fact]
    public async Task Client_and_anonymous_cannot_use_private_operations()
    {
        var (owner, _) = await _api.NewAgentAsync();
        var property = await CreateAsync(owner, Body());
        var (client, _) = await _api.NewClientAsync();
        var anonymous = _api.Anonymous();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/properties", Body(), _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/properties/{property.Id}", Body(), _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/properties/{property.Id}", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/properties/my-properties", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/properties", _ct)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/properties", Body(), _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"/api/properties/{property.Id}", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/properties", _ct)).StatusCode);
    }

    [Fact]
    public async Task Admin_manages_every_property()
    {
        var (owner, ownerId) = await _api.NewAgentAsync();
        var draft = await CreateAsync(owner, Body(status: "Draft"));
        var other = await CreateAsync(owner, Body());
        var admin = await _api.AdminAsync();

        var all = await GetPageAsync(admin, $"/api/admin/properties?status=Draft&pageSize={PropertyService.MaxPageSize}");
        Assert.Contains(all.Items, p => p.Id == draft.Id);

        var update = await admin.PutAsJsonAsync($"/api/admin/properties/{draft.Id}", Body(status: "Sold"), _ct);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<PropertyResponse>(PostgresApiFixture.Json, _ct);
        Assert.Equal(PropertyStatus.Sold, updated!.Status);
        Assert.Equal(ownerId, updated.OwnerId);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/properties/{other.Id}", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/properties/{other.Id}", _ct)).StatusCode);
    }

    [Fact]
    public async Task Agents_cannot_use_admin_endpoints()
    {
        var (agent, _) = await _api.NewAgentAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/api/admin/properties", _ct)).StatusCode);
    }

    [Fact]
    public async Task Public_search_excludes_draft_rented_and_sold()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var available = await CreateAsync(agent, Body(title: $"Visible {tag}"));
        foreach (var status in new[] { "Draft", "Rented", "Sold" })
        {
            await CreateAsync(agent, Body(title: $"{status} {tag}", status: status));
        }

        var page = await GetPageAsync(_api.Anonymous(), $"/api/properties?search={tag}&status=Draft");

        Assert.Equal(available.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Public_filters_are_translated_to_sql()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var match = await CreateAsync(agent, Body(title: $"Maison {tag}", city: "Jacmel", transactionType: "Rent", price: 800));
        await CreateAsync(agent, Body(title: $"Vente {tag}", city: "Jacmel", transactionType: "Sale", price: 800));
        await CreateAsync(agent, Body(title: $"Chère {tag}", city: "Jacmel", transactionType: "Rent", price: 5000));
        await CreateAsync(agent, Body(title: $"Terrain {tag}", city: "Jacmel", transactionType: "Rent", propertyType: "Land", price: 800));
        await CreateAsync(agent, Body(title: $"Ailleurs {tag}", city: "Hinche", transactionType: "Rent", price: 800));

        var page = await GetPageAsync(
            _api.Anonymous(),
            $"/api/properties?search={tag}&location=JACMEL&transactionType=Rent&propertyType=House&minPrice=500&maxPrice=1000");

        Assert.Equal(match.Id, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task Invalid_property_returns_camelCase_validation_errors()
    {
        var (agent, _) = await _api.NewAgentAsync();

        var response = await agent.PostAsJsonAsync("/api/properties", new { title = "", price = -1 }, _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("title", out _));
        Assert.True(errors.TryGetProperty("price", out _));
    }

    [Fact]
    public async Task Admin_role_cannot_be_self_registered()
    {
        var response = await _api.Anonymous().PostAsJsonAsync(
            "/api/auth/register",
            new { email = $"{Guid.NewGuid():N}@imotek.test", password = PostgresApiFixture.UserPassword, fullName = "X", role = Roles.Admin },
            _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
