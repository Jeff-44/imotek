using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Imotek.Infrastructure.Identity;
using Imotek.Infrastructure.Persistence;
using Imotek.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Imotek.Tests.Api;

/// <summary>
/// Access tokens stop working once their session is revoked (logout on any device, approval), instead of
/// living on until they expire. Public endpoints are unaffected. Over HTTP with PostgreSQL.
/// </summary>
[Collection(PostgresApiCollection.Name)]
public class SessionRevocationApiTests
{
    private readonly PostgresApiFixture _api;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public SessionRevocationApiTests(PostgresApiFixture api)
    {
        _api = api;
        _api.SkipIfUnavailable();
    }

    private sealed record Session(HttpClient Client, string RefreshToken);

    private static async Task<Session> SignInAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = PostgresApiFixture.UserPassword });
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());
        return new Session(client, tokens.GetProperty("refreshToken").GetString()!);
    }

    private Task<HttpStatusCode> MeAsync(HttpClient client) =>
        client.GetAsync("/api/auth/me", _ct).ContinueWith(t => t.Result.StatusCode, _ct);

    private Task<HttpStatusCode> RefreshAsync(Session session) =>
        _api.Anonymous().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken }, _ct)
            .ContinueWith(t => t.Result.StatusCode, _ct);

    [Fact]
    public async Task Logout_revokes_the_access_token_at_once_on_every_device()
    {
        var (email, _) = await _api.RegisterAsync();
        var laptop = await SignInAsync(_api.Anonymous(), email);
        var phone = await SignInAsync(_api.Anonymous(), email);
        Assert.Equal(HttpStatusCode.OK, await MeAsync(laptop.Client));
        Assert.Equal(HttpStatusCode.OK, await MeAsync(phone.Client));

        Assert.Equal(HttpStatusCode.NoContent, (await laptop.Client.PostAsync("/api/auth/logout", null, _ct)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(laptop.Client));
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(phone.Client));
        Assert.Equal(HttpStatusCode.Unauthorized, await RefreshAsync(laptop));
        Assert.Equal(HttpStatusCode.Unauthorized, await RefreshAsync(phone));

        // Signing in again gives a working session.
        var again = await SignInAsync(_api.Anonymous(), email);
        Assert.Equal(HttpStatusCode.OK, await MeAsync(again.Client));
        Assert.Equal(HttpStatusCode.OK, await RefreshAsync(again));
    }

    [Fact]
    public async Task Agent_client_and_admin_tokens_work_until_their_logout()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var (client, _) = await _api.NewClientAsync();
        var admin = await _api.AdminAsync();
        var calls = new (HttpClient Client, string Path)[]
        {
            (agent, "/api/properties/my-properties"),
            (client, "/api/requests/mine"),
            (admin, "/api/admin/agent-requests"),
        };

        foreach (var (user, path) in calls)
        {
            Assert.Equal(HttpStatusCode.OK, (await user.GetAsync(path, _ct)).StatusCode);
        }

        // The admin keeps signing in for the other tests: only the agent and the client sign out here.
        await agent.PostAsync("/api/auth/logout", null, _ct);
        await client.PostAsync("/api/auth/logout", null, _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.GetAsync("/api/properties/my-properties", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.PostAsJsonAsync("/api/properties", PropertiesApiTests.Body(), _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/requests/mine", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/agent-requests", _ct)).StatusCode);
    }

    /// <summary>A real access token of <paramref name="email"/>, already expired.</summary>
    private Task<string> ExpiredTokenAsync(string email) => _api.ServiceAsync(async sp =>
    {
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var principal = await sp.GetRequiredService<SignInManager<ApplicationUser>>()
            .CreateUserPrincipalAsync((await users.FindByEmailAsync(email))!);
        var options = sp.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(IdentityConstants.BearerScheme);
        var properties = new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1) };
        return options.BearerTokenProtector.Protect(new AuthenticationTicket(principal, properties, IdentityConstants.BearerScheme));
    });

    private HttpClient WithToken(string token)
    {
        var client = _api.Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(Guid PropertyId, HttpClient Agent)> ListingAsync()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var created = await agent.PostAsJsonAsync("/api/properties", PropertiesApiTests.Body(), _ct);
        return ((await created.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("id").GetGuid(), agent);
    }

    private static object Visit(DateTimeOffset at) => new { type = "Visit", visitAt = at, fullName = "Ne doit pas servir", email = "visiteur@example.com", phoneNumber = "+509 3712 3456" };

    private async Task<int> ReceivedCountAsync(HttpClient agent) =>
        (await agent.GetFromJsonAsync<JsonElement>("/api/requests/received", _ct)).GetArrayLength();

    [Fact]
    public async Task An_expired_revoked_or_unreadable_token_gets_401_on_public_endpoints_too()
    {
        var (propertyId, _) = await ListingAsync();
        var (email, _) = await _api.RegisterAsync();
        var expired = WithToken(await ExpiredTokenAsync(email));
        var revoked = await SignInAsync(_api.Anonymous(), email);
        await (await SignInAsync(_api.Anonymous(), email)).Client.PostAsync("/api/auth/logout", null, _ct);
        var garbage = WithToken("not-a-token");

        foreach (var client in new[] { expired, revoked.Client, garbage })
        {
            foreach (var path in new[] { "/api/properties", $"/api/properties/{propertyId}", $"/api/properties/{propertyId}/contact", "/api/auth/me", "/api/requests/mine" })
            {
                var response = await client.GetAsync(path, _ct);
                Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{path}: {(int)response.StatusCode}");
                Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString());
            }
        }

        // A true visitor, who sends no token, is served as before.
        Assert.Equal(HttpStatusCode.OK, (await _api.Anonymous().GetAsync("/api/properties", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.Anonymous().GetAsync($"/api/properties/{propertyId}/contact", _ct)).StatusCode);
    }

    [Fact]
    public async Task A_client_request_with_a_stale_token_is_refused_never_turned_into_a_visitor_request()
    {
        var (propertyId, agent) = await ListingAsync();
        var (email, _) = await _api.RegisterAsync();
        var expired = WithToken(await ExpiredTokenAsync(email));
        var revoked = await SignInAsync(_api.Anonymous(), email);
        await (await SignInAsync(_api.Anonymous(), email)).Client.PostAsync("/api/auth/logout", null, _ct);
        var at = DateTimeOffset.UtcNow.AddDays(3);

        // Even with every visitor field present, the stale token makes it a 401 and nothing is stored.
        Assert.Equal(HttpStatusCode.Unauthorized, (await expired.PostAsJsonAsync($"/api/properties/{propertyId}/requests", Visit(at), _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await revoked.Client.PostAsJsonAsync($"/api/properties/{propertyId}/requests", Visit(at), _ct)).StatusCode);
        Assert.Equal(0, await ReceivedCountAsync(agent));

        // Signed in again, the same request is a client's request, stored once.
        var fresh = await SignInAsync(_api.Anonymous(), email);
        var sent = await fresh.Client.PostAsJsonAsync($"/api/properties/{propertyId}/requests", new { type = "Visit", visitAt = at }, _ct);
        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        var stored = (await sent.Content.ReadFromJsonAsync<JsonElement>(_ct));
        Assert.False(stored.GetProperty("isGuest").GetBoolean());
        Assert.Equal(1, await ReceivedCountAsync(agent));

        // A visitor without a token still sends a visitor request.
        var visitor = await _api.Anonymous().PostAsJsonAsync($"/api/properties/{propertyId}/requests", Visit(at.AddDays(1)), _ct);
        Assert.Equal(HttpStatusCode.Created, visitor.StatusCode);
        Assert.True((await visitor.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("isGuest").GetBoolean());
    }

    [Fact]
    public async Task A_valid_token_works_on_public_and_private_endpoints()
    {
        var (propertyId, _) = await ListingAsync();
        var (email, _) = await _api.RegisterAsync();
        var session = await SignInAsync(_api.Anonymous(), email);

        foreach (var path in new[] { "/api/properties", $"/api/properties/{propertyId}", $"/api/properties/{propertyId}/contact", "/api/auth/me", "/api/requests/mine" })
        {
            Assert.Equal(HttpStatusCode.OK, (await session.Client.GetAsync(path, _ct)).StatusCode);
        }
    }

    [Fact]
    public async Task Approval_revokes_the_pending_users_access_token_too()
    {
        var (email, userId) = await _api.RegisterAsync(requestAgent: true);
        var pending = await SignInAsync(_api.Anonymous(), email);
        Assert.Equal(HttpStatusCode.OK, await MeAsync(pending.Client));

        (await (await _api.AdminAsync()).PostAsync($"/api/admin/agent-requests/{userId}/approve", null, _ct)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(pending.Client));
        var agent = await SignInAsync(_api.Anonymous(), email);
        Assert.Equal(HttpStatusCode.OK, (await agent.Client.GetAsync("/api/properties/my-properties", _ct)).StatusCode);
    }

    /// <summary>Counts the security stamp lookups the check sends to PostgreSQL.</summary>
    private sealed class StampQueryCounter : DbCommandInterceptor
    {
        public int Count;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("\"SecurityStamp\"", StringComparison.Ordinal)
                && !command.CommandText.Contains("\"PasswordHash\"", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref Count);
            }

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task One_database_lookup_per_user_and_cache_lifetime_and_changes_from_elsewhere_are_seen_after_it()
    {
        var counter = new StampQueryCounter();
        await using var host = _api.Derive(builder =>
        {
            builder.UseSetting("Auth:SecurityStampCache:Seconds", "2");
            builder.ConfigureTestServices(services => services.ConfigureDbContext<ImotekDbContext>(o => o.AddInterceptors(counter)));
        });
        var (email, userId) = await _api.RegisterAsync();
        var session = await SignInAsync(host.CreateClient(), email);

        counter.Count = 0;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(HttpStatusCode.OK, await MeAsync(session.Client));
        }

        clock.Stop();
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), "the 100 calls must fit in one cache lifetime for this count");
        Assert.Equal(1, counter.Count);

        // A stamp changed without going through this instance (another server, the database): still
        // accepted while cached, refused once the cached value expires.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(userId.ToString()))!;
            // Straight through the store, as another server would: this instance's cache is not told.
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            await users.UpdateAsync(user);
        }
        Assert.Equal(HttpStatusCode.OK, await MeAsync(session.Client));
        await Task.Delay(TimeSpan.FromSeconds(2.2), _ct);
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(session.Client));
    }
}
