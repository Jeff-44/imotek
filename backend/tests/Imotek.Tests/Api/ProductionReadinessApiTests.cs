using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Imotek.Application.Common;
using Imotek.Application.Properties;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Api;

/// <summary>Rules added for production: hidden statuses, accent search, currency, uploads, token refresh.</summary>
[Collection(PostgresApiCollection.Name)]
public class ProductionReadinessApiTests
{
    private readonly PostgresApiFixture _api;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public ProductionReadinessApiTests(PostgresApiFixture api)
    {
        _api = api;
        _api.SkipIfUnavailable();
    }

    private async Task<PropertyResponse> CreateAsync(HttpClient agent, object body)
    {
        var response = await agent.PostAsJsonAsync("/api/properties", body, _ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PropertyResponse>(PostgresApiFixture.Json, _ct))!;
    }

    private Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid propertyId, byte[] content, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return client.PostAsync($"/api/properties/{propertyId}/images", form, _ct);
    }

    // ---- Hidden statuses ----------------------------------------------------

    [Theory]
    [InlineData("Draft")]
    [InlineData("Rented")]
    [InlineData("Sold")]
    public async Task Client_cannot_read_hidden_property_by_id(string status)
    {
        var (agent, _) = await _api.NewAgentAsync();
        var property = await CreateAsync(agent, PropertiesApiTests.Body(status: status));
        var (client, _) = await _api.NewClientAsync();

        var asClient = await client.GetAsync($"/api/properties/{property.Id}", _ct);
        var asVisitor = await _api.Anonymous().GetAsync($"/api/properties/{property.Id}", _ct);
        var asOwner = await agent.GetAsync($"/api/properties/{property.Id}", _ct);

        Assert.Equal(HttpStatusCode.NotFound, asClient.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asVisitor.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asOwner.StatusCode);
        Assert.DoesNotContain(property.Title, await asClient.Content.ReadAsStringAsync(_ct));
    }

    // ---- Search -------------------------------------------------------------

    [Theory]
    [InlineData("petion")]
    [InlineData("PÉTION-VILLE")]
    public async Task Accent_insensitive_location_search_runs_in_sql(string term)
    {
        var (agent, _) = await _api.NewAgentAsync();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var property = await CreateAsync(agent, PropertiesApiTests.Body(title: $"Accent {tag}", city: "Pétion-Ville"));

        var page = await _api.Anonymous().GetFromJsonAsync<PagedResult<PropertyResponse>>(
            $"/api/properties?search={tag}&location={Uri.EscapeDataString(term)}", PostgresApiFixture.Json, _ct);

        Assert.Equal(property.Id, Assert.Single(page!.Items).Id);
    }

    [Fact]
    public async Task Price_filter_without_currency_returns_400()
    {
        var response = await _api.Anonymous().GetAsync("/api/properties?minPrice=100", _ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("currency", out _));
    }

    // ---- Uploads ------------------------------------------------------------

    [Fact]
    public async Task Owner_uploads_photo_served_from_media_without_metadata()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var property = await CreateAsync(agent, PropertiesApiTests.Body());

        var upload = await UploadAsync(agent, property.Id, TestImages.JpegWithExif(), "IMG_0001.JPG", "image/jpeg");

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var updated = await upload.Content.ReadFromJsonAsync<PropertyResponse>(PostgresApiFixture.Json, _ct);
        var photo = Assert.Single(updated!.Photos);
        Assert.StartsWith("/media/properties/", photo.Url);

        var media = await _api.Anonymous().GetAsync(photo.Url, _ct);
        Assert.Equal(HttpStatusCode.OK, media.StatusCode);
        Assert.Equal("image/jpeg", media.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", media.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.False(TestImages.Contains(await media.Content.ReadAsByteArrayAsync(_ct), TestImages.GpsMarker));

        var delete = await agent.DeleteAsync($"/api/properties/{property.Id}/images/{photo.Id}", _ct);
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _api.Anonymous().GetAsync(photo.Url, _ct)).StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_non_images_and_unauthorized_callers()
    {
        var (owner, _) = await _api.NewAgentAsync();
        var property = await CreateAsync(owner, PropertiesApiTests.Body());
        var (otherAgent, _) = await _api.NewAgentAsync();
        var (client, _) = await _api.NewClientAsync();

        var svg = await UploadAsync(owner, property.Id, TestImages.Svg(), "photo.jpg", "image/jpeg");
        var byOther = await UploadAsync(otherAgent, property.Id, TestImages.JpegWithExif(), "a.jpg", "image/jpeg");
        var byClient = await UploadAsync(client, property.Id, TestImages.JpegWithExif(), "a.jpg", "image/jpeg");
        var anonymous = await UploadAsync(_api.Anonymous(), property.Id, TestImages.JpegWithExif(), "a.jpg", "image/jpeg");

        Assert.Equal(HttpStatusCode.BadRequest, svg.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byOther.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byClient.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Upload_larger_than_the_request_limit_is_refused()
    {
        var (owner, _) = await _api.NewAgentAsync();
        var property = await CreateAsync(owner, PropertiesApiTests.Body());

        var response = await UploadAsync(owner, property.Id, new byte[7 * 1024 * 1024], "big.jpg", "image/jpeg");

        Assert.True(response.StatusCode is HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.BadRequest, response.StatusCode.ToString());
    }

    // ---- Tokens -------------------------------------------------------------

    private async Task<JsonElement> LoginTokensAsync(string email)
    {
        var login = await _api.Anonymous().PostAsJsonAsync(
            "/api/auth/login", new { email, password = PostgresApiFixture.UserPassword }, _ct);
        login.EnsureSuccessStatusCode();
        return await login.Content.ReadFromJsonAsync<JsonElement>(_ct);
    }

    private async Task<string> RegisterClientAsync()
    {
        var email = $"refresh-{Guid.NewGuid():N}@imotek.test";
        var register = await _api.Anonymous().PostAsJsonAsync(
            "/api/auth/register", new { email, password = PostgresApiFixture.UserPassword, fullName = "Refresh", role = Roles.Client }, _ct);
        register.EnsureSuccessStatusCode();
        return email;
    }

    [Fact]
    public async Task Refresh_token_returns_a_working_access_token()
    {
        var tokens = await LoginTokensAsync(await RegisterClientAsync());

        var refresh = await _api.Anonymous().PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = tokens.GetProperty("refreshToken").GetString() }, _ct);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var renewed = await refresh.Content.ReadFromJsonAsync<JsonElement>(_ct);
        var client = _api.Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", renewed.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me", _ct)).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task Invalid_refresh_token_is_rejected(string token)
    {
        var refresh = await _api.Anonymous().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token }, _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Access_token_cannot_be_used_as_refresh_token()
    {
        var tokens = await LoginTokensAsync(await RegisterClientAsync());

        var refresh = await _api.Anonymous().PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = tokens.GetProperty("accessToken").GetString() }, _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_tokens()
    {
        var tokens = await LoginTokensAsync(await RegisterClientAsync());
        var client = _api.Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null, _ct)).StatusCode);

        var refresh = await _api.Anonymous().PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = tokens.GetProperty("refreshToken").GetString() }, _ct);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Tokens_expire_and_access_token_lifetime_is_short()
    {
        var tokens = await LoginTokensAsync(await RegisterClientAsync());

        var expiresIn = tokens.GetProperty("expiresIn").GetInt32();
        Assert.InRange(expiresIn, 60, 60 * 60);
    }
}
