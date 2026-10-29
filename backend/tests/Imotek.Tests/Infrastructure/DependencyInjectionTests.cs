using Imotek.Infrastructure;
using Imotek.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Imotek.Tests.Infrastructure;

public class DependencyInjectionTests
{
    // Never opened: these tests only build the service graph.
    private const string FakeConnectionString = "Host=localhost;Database=imotek_test;Username=test";

    private static IConfiguration BuildConfiguration(string? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DependencyInjection.ConnectionStringName}"] = connectionString,
            })
            .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddInfrastructure_throws_when_connection_string_is_missing(string? connectionString)
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(BuildConfiguration(connectionString)));

        Assert.Contains(DependencyInjection.ConnectionStringName, ex.Message);
    }

    [Fact]
    public void AddInfrastructure_registers_DbContext_with_Npgsql_provider()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(BuildConfiguration(FakeConnectionString));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ImotekDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Equal(FakeConnectionString, context.Database.GetConnectionString());
    }

    [Fact]
    public void AddInfrastructure_registers_tagged_database_health_check()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(BuildConfiguration(FakeConnectionString));

        using var provider = services.BuildServiceProvider();
        var registrations = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        var registration = Assert.Single(registrations, r => r.Name == DependencyInjection.DatabaseHealthCheckName);
        Assert.Contains(DependencyInjection.DatabaseHealthCheckTag, registration.Tags);
    }
}
