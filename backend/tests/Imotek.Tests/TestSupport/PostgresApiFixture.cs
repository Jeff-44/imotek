using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Imotek.Application.Common;
using Imotek.Infrastructure;
using Imotek.Infrastructure.Identity;
using Imotek.Infrastructure.Persistence;
using Imotek.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
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

    /// <summary>Throw-away upload directory, deleted with the fixture.</summary>
    public string UploadRoot { get; } = Path.Combine(Path.GetTempPath(), "imotek-tests", Guid.NewGuid().ToString("N"));

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
            // Every test client shares one address; the limiter itself is covered by SecurityTests.
            builder.UseSetting("RateLimiting:AuthPermitLimit", "100000");
            builder.UseSetting(FileStorageOptions.SectionName + ":RootPath", UploadRoot);
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

        if (Directory.Exists(UploadRoot))
        {
            Directory.Delete(UploadRoot, recursive: true);
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

    /// <summary>A valid Haitian mobile number, distinct per call, for agent requests.</summary>
    public static string NewHaitianPhone() => $"+5093{Random.Shared.Next(1000000, 9999999)}";

    /// <summary>Registers a client (optionally with a pending agent request); returns the email and id.</summary>
    public async Task<(string Email, Guid UserId)> RegisterAsync(bool requestAgent = false)
    {
        var email = $"{(requestAgent ? "agent" : "client")}-{Guid.NewGuid():N}@imotek.test";
        var register = await Anonymous().PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = UserPassword,
            fullName = requestAgent ? "Test Agent" : "Test Client",
            requestAgent,
            phoneNumber = requestAgent ? NewHaitianPhone() : null,
            phonePublicConsent = requestAgent,
        });
        register.EnsureSuccessStatusCode();
        var user = await register.Content.ReadFromJsonAsync<JsonElement>();
        return (email, user.GetProperty("id").GetGuid());
    }

    public Task<HttpClient> AdminAsync() => LoginAsync(AdminEmail, AdminPassword);

    /// <summary>An approved agent, through the real workflow: request at registration, admin approval, then sign-in.</summary>
    public async Task<(HttpClient Client, Guid UserId)> NewAgentAsync()
    {
        var (email, userId) = await RegisterAsync(requestAgent: true);
        var admin = await AdminAsync();
        (await admin.PostAsync($"/api/admin/agent-requests/{userId}/approve", null)).EnsureSuccessStatusCode();
        return (await LoginAsync(email, UserPassword), userId);
    }

    public async Task<(HttpClient Client, Guid UserId)> NewClientAsync()
    {
        var (email, userId) = await RegisterAsync();
        return (await LoginAsync(email, UserPassword), userId);
    }

    /// <summary>
    /// An account created before the approval workflow: Agent role, AgentStatus None, no phone —
    /// like example@gmail.com. Optionally owns one available listing written directly in the database.
    /// </summary>
    public async Task<(HttpClient Client, Guid UserId, Guid? PropertyId)> NewLegacyAgentAsync(bool withListing = false)
    {
        var email = $"legacy-{Guid.NewGuid():N}@imotek.test";
        var (userId, propertyId) = await AsyncService(async sp =>
        {
            var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, FullName = "Legacy Agent" };
            (await users.CreateAsync(user, UserPassword)).ThrowIfFailed();
            (await users.AddToRoleAsync(user, Roles.Agent)).ThrowIfFailed();

            Guid? listingId = null;
            if (withListing)
            {
                var db = sp.GetRequiredService<ImotekDbContext>();
                var property = PropertyTestData.Property(user.Id, PropertyTestData.Details(title: "Annonce héritée"), DateTime.UtcNow);
                db.Properties.Add(property);
                await db.SaveChangesAsync();
                listingId = property.Id;
            }

            return (user.Id, listingId);
        });

        return (await LoginAsync(email, UserPassword), userId, propertyId);
    }

    private async Task<T> AsyncService<T>(Func<IServiceProvider, Task<T>> use)
    {
        using var scope = _factory!.Services.CreateScope();
        return await use(scope.ServiceProvider);
    }
}

internal static class IdentityResultExtensions
{
    public static void ThrowIfFailed(this IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Code)));
        }
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresApiCollection : ICollectionFixture<PostgresApiFixture>
{
    public const string Name = "PostgreSQL API";
}
