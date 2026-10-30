using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Imotek.Application.Common;
using Imotek.Infrastructure;
using Imotek.Infrastructure.Identity;
using Imotek.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Imotek.Tests.TestSupport;

/// <summary>
/// Runs the real API against a throw-away PostgreSQL container, with the EF Core
/// migrations applied. Tests are skipped (not failed) when Docker is not available.
/// </summary>
public sealed class PostgresApiFixture : IAsyncLifetime
{
    public const string AdminEmail = "admin@imotek.test";
    public const string AdminPassword = "Admin-Passw0rd";
    public const string UserPassword = "Passw0rd-123";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private PostgreSqlContainer? _container;
    private WebApplicationFactory<Program>? _factory;

    public string? SkipReason { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder("postgres:18-alpine").Build();
            await _container.StartAsync();
        }
        catch (Exception ex)
        {
            SkipReason = $"Docker is not available for PostgreSQL integration tests: {ex.GetType().Name}: {ex.Message}";
            return;
        }

        var connectionString = _container.GetConnectionString();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", connectionString);
            builder.UseSetting(AdminSeeder.EmailKey, AdminEmail);
            builder.UseSetting(AdminSeeder.PasswordKey, AdminPassword);
            builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
        });

        // Migrations must exist before the host starts, because startup seeds the admin.
        var options = new DbContextOptionsBuilder<ImotekDbContext>().UseNpgsql(connectionString).Options;
        await using (var db = new ImotekDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        _ = _factory.Server;
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public void SkipIfUnavailable() => Assert.SkipWhen(SkipReason is not null, SkipReason ?? string.Empty);

    public HttpClient Anonymous() => _factory!.CreateClient();

    public T Service<T>(Func<IServiceProvider, T> use)
    {
        using var scope = _factory!.Services.CreateScope();
        return use(scope.ServiceProvider);
    }

    public async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = Anonymous();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.GetProperty("accessToken").GetString());
        return client;
    }

    /// <summary>Registers a new user with a unique email and returns a client authenticated as that user.</summary>
    public async Task<(HttpClient Client, Guid UserId)> NewUserAsync(string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@imotek.test";
        var register = await Anonymous().PostAsJsonAsync(
            "/api/auth/register", new { email, password = UserPassword, fullName = $"Test {role}", role });
        register.EnsureSuccessStatusCode();
        var user = await register.Content.ReadFromJsonAsync<JsonElement>();

        return (await LoginAsync(email, UserPassword), user.GetProperty("id").GetGuid());
    }

    public Task<HttpClient> AdminAsync() => LoginAsync(AdminEmail, AdminPassword);

    public Task<(HttpClient Client, Guid UserId)> NewAgentAsync() => NewUserAsync(Roles.Agent);

    public Task<(HttpClient Client, Guid UserId)> NewClientAsync() => NewUserAsync(Roles.Client);
}

[CollectionDefinition(Name)]
public sealed class PostgresApiCollection : ICollectionFixture<PostgresApiFixture>
{
    public const string Name = "PostgreSQL API";
}
