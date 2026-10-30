using System.Net;
using System.Net.Http.Json;
using Imotek.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Imotek.Tests.Api;

/// <summary>HTTP hardening that needs no database: rate limiting, headers, CORS.</summary>
public class SecurityTests(SecurityTests.Factory factory) : IClassFixture<SecurityTests.Factory>
{
    public const int AuthLimit = 3;
    public const string AllowedOrigin = "https://app.imotek.test";

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}",
                "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1");
            builder.UseSetting("RateLimiting:AuthPermitLimit", AuthLimit.ToString());
            builder.UseSetting("Cors:AllowedOrigins:0", AllowedOrigin);
        }
    }

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Auth_endpoints_are_rate_limited()
    {
        var client = factory.CreateClient();

        // An empty registration fails validation before touching the database, so only the limiter matters.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < AuthLimit + 2; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/auth/register", new { }, _ct)).StatusCode);
        }

        Assert.All(statuses.Take(AuthLimit), s => Assert.Equal(HttpStatusCode.BadRequest, s));
        Assert.All(statuses.Skip(AuthLimit), s => Assert.Equal(HttpStatusCode.TooManyRequests, s));
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await factory.CreateClient().GetAsync("/health", _ct);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Cors_allows_only_configured_origins()
    {
        var client = factory.CreateClient();

        async Task<HttpResponseMessage> Preflight(string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/api/properties");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "GET");
            return await client.SendAsync(request, _ct);
        }

        var allowed = await Preflight(AllowedOrigin);
        var denied = await Preflight("https://evil.example");

        Assert.Equal(AllowedOrigin, allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("/media/../appsettings.json")]
    [InlineData("/media/properties/x/y.jpg")]
    [InlineData("/media/properties/0123456789abcdef0123456789abcdef/0123456789abcdef0123456789abcdef.html")]
    public async Task Media_only_serves_generated_image_keys(string path)
    {
        var response = await factory.CreateClient().GetAsync(path, _ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Wildcard_cors_origin_is_refused_at_startup()
    {
        using var misconfigured = factory.WithWebHostBuilder(b => b.UseSetting("Cors:AllowedOrigins:0", "*"));

        var ex = Assert.ThrowsAny<Exception>(() => misconfigured.CreateClient());
        Assert.Contains("Cors:AllowedOrigins", ex.ToString());
    }
}
