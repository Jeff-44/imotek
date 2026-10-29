using System.Net;
using System.Text.Json;
using Imotek.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Imotek.Tests.Api;

// Runs the API in memory against a database address where nothing listens,
// so the unavailable-database path is tested without a real PostgreSQL.
public class HealthEndpointsTests(HealthEndpointsTests.UnreachableDatabaseFactory factory)
    : IClassFixture<HealthEndpointsTests.UnreachableDatabaseFactory>
{
    private const string UnreachableConnectionString =
        "Host=127.0.0.1;Port=1;Database=imotek_test;Username=test;Password=not-a-real-secret;Timeout=2";

    public sealed class UnreachableDatabaseFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // "Testing" skips appsettings.Development.json and user-secrets.
            builder.UseEnvironment("Testing");
            builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", UnreachableConnectionString);
        }
    }

    [Fact]
    public async Task Health_returns_200_without_checking_the_database()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await factory.CreateClient().GetAsync("/health", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, body.RootElement.GetProperty("checks").GetArrayLength());
    }

    [Fact]
    public async Task HealthDb_returns_503_when_database_is_unreachable()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await factory.CreateClient().GetAsync("/health/db", ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.Equal("Unhealthy", body.RootElement.GetProperty("status").GetString());
        var check = Assert.Single(body.RootElement.GetProperty("checks").EnumerateArray());
        Assert.Equal(DependencyInjection.DatabaseHealthCheckName, check.GetProperty("name").GetString());
        Assert.Equal("Unhealthy", check.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HealthDb_does_not_leak_connection_details()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await factory.CreateClient().GetAsync("/health/db", ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        Assert.DoesNotContain("not-a-real-secret", content);
        Assert.DoesNotContain("Password", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("127.0.0.1", content);
        Assert.DoesNotContain("Exception", content, StringComparison.OrdinalIgnoreCase);
    }
}
