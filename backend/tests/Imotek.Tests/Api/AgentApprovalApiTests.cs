using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Imotek.Application.Auth;
using Imotek.Application.Common;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Api;

/// <summary>Step 1 over HTTP with PostgreSQL: registration, agent requests, admin decisions, legacy agents, profile.</summary>
[Collection(PostgresApiCollection.Name)]
public class AgentApprovalApiTests
{
    private readonly PostgresApiFixture _api;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public AgentApprovalApiTests(PostgresApiFixture api)
    {
        _api = api;
        _api.SkipIfUnavailable();
    }

    private static string NewEmail() => $"step1-{Guid.NewGuid():N}@imotek.test";

    private Task<HttpResponseMessage> Register(object body) => _api.Anonymous().PostAsJsonAsync("/api/auth/register", body, _ct);

    private async Task<UserResponse> MeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<UserResponse>("/api/auth/me", PostgresApiFixture.Json, _ct))!;

    private static object Listing() => PropertiesApiTests.Body();

    private static async Task<JsonElement> ErrorsOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");

    // ---- Registration -------------------------------------------------------

    [Fact]
    public async Task Registration_creates_a_client_by_default()
    {
        var email = NewEmail();
        var response = await Register(new { email, password = PostgresApiFixture.UserPassword, fullName = "Cliente" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = (await response.Content.ReadFromJsonAsync<UserResponse>(PostgresApiFixture.Json, _ct))!;
        Assert.Equal([Roles.Client], user.Roles);
        Assert.Equal(AgentStatus.None, user.AgentStatus);
        Assert.Null(user.PhoneNumber); // optional for clients
        Assert.False(user.CanPublish);
    }

    [Fact]
    public async Task Client_phone_is_optional_but_validated_and_normalized()
    {
        var ok = await Register(new { email = NewEmail(), password = PostgresApiFixture.UserPassword, fullName = "C", phoneNumber = "+1 (650) 253-0000" });
        var bad = await Register(new { email = NewEmail(), password = PostgresApiFixture.UserPassword, fullName = "C", phoneNumber = "+509 12" });

        Assert.Equal("+16502530000", (await ok.Content.ReadFromJsonAsync<UserResponse>(PostgresApiFixture.Json, _ct))!.PhoneNumber);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.True((await ErrorsOf(bad)).TryGetProperty("phoneNumber", out _));
    }

    [Fact]
    public async Task Agent_request_at_registration_is_pending_without_the_agent_role()
    {
        var email = NewEmail();
        var response = await Register(new
        {
            email, password = PostgresApiFixture.UserPassword, fullName = "Futur agent",
            requestAgent = true, phoneNumber = "3712 3456", phonePublicConsent = true, agentRequestMessage = "Agence à Delmas",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = (await response.Content.ReadFromJsonAsync<UserResponse>(PostgresApiFixture.Json, _ct))!;
        Assert.Equal(AgentStatus.Pending, user.AgentStatus);
        Assert.Equal([Roles.Client], user.Roles);
        Assert.Equal("+50937123456", user.PhoneNumber);
        Assert.NotNull(user.AgentRequestedAt);

        // The pending agent's own token has no Agent role: publishing is refused by the API.
        var client = await _api.LoginAsync(email, PostgresApiFixture.UserPassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/properties", Listing(), _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/properties/my-properties", _ct)).StatusCode);
    }

    [Fact]
    public async Task Agent_request_requires_a_valid_phone_and_consent()
    {
        var noPhone = await Register(new { email = NewEmail(), password = PostgresApiFixture.UserPassword, fullName = "A", requestAgent = true, phonePublicConsent = true });
        var noConsent = await Register(new { email = NewEmail(), password = PostgresApiFixture.UserPassword, fullName = "A", requestAgent = true, phoneNumber = "+50937123456" });
        var badPhone = await Register(new { email = NewEmail(), password = PostgresApiFixture.UserPassword, fullName = "A", requestAgent = true, phoneNumber = "+999 1234", phonePublicConsent = true });

        Assert.True((await ErrorsOf(noPhone)).TryGetProperty("phoneNumber", out _));
        Assert.True((await ErrorsOf(noConsent)).TryGetProperty("phonePublicConsent", out _));
        Assert.True((await ErrorsOf(badPhone)).TryGetProperty("phoneNumber", out _));
    }

    [Theory]
    [InlineData("Agent", HttpStatusCode.Created)]   // legacy field: becomes a pending request, never the role
    [InlineData("Client", HttpStatusCode.Created)]
    [InlineData("Admin", HttpStatusCode.BadRequest)]
    public async Task Legacy_role_field_cannot_grant_a_role(string role, HttpStatusCode expected)
    {
        var response = await Register(new
        {
            email = NewEmail(), password = PostgresApiFixture.UserPassword, fullName = "X", role,
            phoneNumber = PostgresApiFixture.NewHaitianPhone(), phonePublicConsent = true,
        });

        Assert.Equal(expected, response.StatusCode);
        if (response.IsSuccessStatusCode)
        {
            var user = (await response.Content.ReadFromJsonAsync<UserResponse>(PostgresApiFixture.Json, _ct))!;
            Assert.Equal([Roles.Client], user.Roles);
        }
    }

    // ---- Admin decisions ----------------------------------------------------

    [Fact]
    public async Task Admin_sees_and_approves_a_request_which_adds_Agent_next_to_Client()
    {
        var (email, userId) = await _api.RegisterAsync(requestAgent: true);
        var oldSession = await LoginTokensAsync(email);
        var admin = await _api.AdminAsync();

        var pending = await admin.GetFromJsonAsync<List<AgentAccountResponse>>("/api/admin/agent-requests", PostgresApiFixture.Json, _ct);
        Assert.Contains(pending!, a => a.Id == userId && a.AgentStatus == AgentStatus.Pending);

        var approve = await admin.PostAsync($"/api/admin/agent-requests/{userId}/approve", null, _ct);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var decided = (await approve.Content.ReadFromJsonAsync<AgentAccountResponse>(PostgresApiFixture.Json, _ct))!;
        Assert.Equal(AgentStatus.Approved, decided.AgentStatus);
        Assert.Equal([Roles.Agent, Roles.Client], decided.Roles.Order());
        Assert.NotNull(decided.AgentReviewedAt);

        // Role change => security stamp updated => sessions issued before approval are revoked.
        var refresh = await _api.Anonymous().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = oldSession.GetProperty("refreshToken").GetString() }, _ct);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        // A new sign-in carries the Agent role and the account can publish.
        var agent = await _api.LoginAsync(email, PostgresApiFixture.UserPassword);
        Assert.True((await MeAsync(agent)).CanPublish);
        Assert.Equal(HttpStatusCode.Created, (await agent.PostAsJsonAsync("/api/properties", Listing(), _ct)).StatusCode);
    }

    [Fact]
    public async Task Admin_rejects_with_a_reason_and_the_user_may_ask_again()
    {
        var (email, userId) = await _api.RegisterAsync(requestAgent: true);
        var admin = await _api.AdminAsync();

        var noReason = await admin.PostAsJsonAsync($"/api/admin/agent-requests/{userId}/reject", new { reason = " " }, _ct);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var reject = await admin.PostAsJsonAsync($"/api/admin/agent-requests/{userId}/reject", new { reason = "Pièces justificatives manquantes" }, _ct);
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        var user = await _api.LoginAsync(email, PostgresApiFixture.UserPassword);
        var me = await MeAsync(user);
        Assert.Equal(AgentStatus.Rejected, me.AgentStatus);
        Assert.Equal("Pièces justificatives manquantes", me.AgentRejectionReason);
        Assert.DoesNotContain(Roles.Agent, me.Roles);

        // Deciding twice is refused; a new request reopens the case.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/admin/agent-requests/{userId}/approve", null, _ct)).StatusCode);
        var again = await user.PostAsJsonAsync("/api/auth/agent-request", new { phoneNumber = PostgresApiFixture.NewHaitianPhone(), phonePublicConsent = true, message = "Dossier complété" }, _ct);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(AgentStatus.Pending, (await MeAsync(user)).AgentStatus);
    }

    [Fact]
    public async Task Non_admins_cannot_list_approve_or_reject()
    {
        var (_, targetId) = await _api.RegisterAsync(requestAgent: true);
        var (client, _) = await _api.NewClientAsync();
        var (agent, _) = await _api.NewAgentAsync();

        foreach (var caller in new[] { client, agent })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await caller.GetAsync("/api/admin/agent-requests", _ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await caller.PostAsync($"/api/admin/agent-requests/{targetId}/approve", null, _ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await caller.PostAsJsonAsync($"/api/admin/agent-requests/{targetId}/reject", new { reason = "x" }, _ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await _api.Anonymous().PostAsync($"/api/admin/agent-requests/{targetId}/approve", null, _ct)).StatusCode);
    }

    [Fact]
    public async Task A_pending_user_cannot_approve_their_own_request()
    {
        var (email, userId) = await _api.RegisterAsync(requestAgent: true);
        var self = await _api.LoginAsync(email, PostgresApiFixture.UserPassword);

        Assert.Equal(HttpStatusCode.Forbidden, (await self.PostAsync($"/api/admin/agent-requests/{userId}/approve", null, _ct)).StatusCode);
        Assert.Equal(AgentStatus.Pending, (await MeAsync(self)).AgentStatus);
    }

    [Fact]
    public async Task An_admin_cannot_decide_on_their_own_account_and_cannot_request_the_role()
    {
        var admin = await _api.AdminAsync();
        var adminId = (await MeAsync(admin)).Id;

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync($"/api/admin/agent-requests/{adminId}/approve", null, _ct)).StatusCode);
        var request = await admin.PostAsJsonAsync("/api/auth/agent-request", new { phoneNumber = PostgresApiFixture.NewHaitianPhone(), phonePublicConsent = true }, _ct);
        Assert.Equal(HttpStatusCode.Forbidden, request.StatusCode);
    }

    [Fact]
    public async Task A_client_cannot_give_themselves_the_agent_role()
    {
        var (client, _) = await _api.NewClientAsync();

        // The profile endpoint has no role field; extra JSON is ignored and nothing changes.
        await client.PutAsJsonAsync("/api/auth/me", new { fullName = "Moi", roles = new[] { "Agent", "Admin" }, agentStatus = "Approved" }, _ct);
        var me = await MeAsync(client);

        Assert.Equal([Roles.Client], me.Roles);
        Assert.Equal(AgentStatus.None, me.AgentStatus);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/properties", Listing(), _ct)).StatusCode);
    }

    // ---- Legacy agents (like example@gmail.com) ------------------------------

    [Fact]
    public async Task Legacy_agent_is_listed_for_review_but_cannot_publish_or_edit()
    {
        var (legacy, legacyId, propertyId) = await _api.NewLegacyAgentAsync(withListing: true);
        var admin = await _api.AdminAsync();

        var listed = await admin.GetFromJsonAsync<List<AgentAccountResponse>>("/api/admin/agent-requests?status=None", PostgresApiFixture.Json, _ct);
        var entry = Assert.Single(listed!, a => a.Id == legacyId);
        Assert.True(entry.IsLegacyAgent);
        Assert.Equal(AgentStatus.None, entry.AgentStatus);

        var me = await MeAsync(legacy);
        Assert.True(me.IsLegacyAgent);
        Assert.False(me.CanPublish);

        // Cannot create or edit content…
        Assert.Equal(HttpStatusCode.Forbidden, (await legacy.PostAsJsonAsync("/api/properties", Listing(), _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await legacy.PutAsJsonAsync($"/api/properties/{propertyId}", Listing(), _ct)).StatusCode);

        // …but still sees what it owns, and the public still sees the existing listing unchanged.
        var mine = await legacy.GetAsync("/api/properties/my-properties", _ct);
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        var publicView = await _api.Anonymous().GetFromJsonAsync<JsonElement>($"/api/properties/{propertyId}", _ct);
        Assert.Equal("Annonce héritée", publicView.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Legacy_agent_needs_a_phone_before_an_admin_can_approve_it()
    {
        var (legacy, legacyId, _) = await _api.NewLegacyAgentAsync();
        var admin = await _api.AdminAsync();

        var withoutPhone = await admin.PostAsync($"/api/admin/agent-requests/{legacyId}/approve", null, _ct);
        Assert.Equal(HttpStatusCode.BadRequest, withoutPhone.StatusCode);

        // Nothing was changed by the refused approval.
        var me = await MeAsync(legacy);
        Assert.Equal(AgentStatus.None, me.AgentStatus);
        Assert.Equal([Roles.Agent], me.Roles);

        // After the account adds a phone with consent, the admin can decide.
        var profile = await legacy.PutAsJsonAsync("/api/auth/me", new { fullName = "Legacy Agent", phoneNumber = "+509 4812 3456", phonePublicConsent = true }, _ct);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/admin/agent-requests/{legacyId}/approve", null, _ct)).StatusCode);
    }

    // ---- Profile ------------------------------------------------------------

    [Fact]
    public async Task Profile_update_normalizes_the_phone_agents_keep_one_and_may_withdraw_consent()
    {
        var (agent, _) = await _api.NewAgentAsync();

        var changed = await agent.PutAsJsonAsync("/api/auth/me", new { fullName = "Agent Renommé", phoneNumber = "0016502530000", phonePublicConsent = true }, _ct);
        Assert.Equal("+16502530000", (await changed.Content.ReadFromJsonAsync<UserResponse>(PostgresApiFixture.Json, _ct))!.PhoneNumber);

        var removed = await agent.PutAsJsonAsync("/api/auth/me", new { fullName = "Agent Renommé", phoneNumber = "", phonePublicConsent = true }, _ct);
        Assert.Equal(HttpStatusCode.BadRequest, removed.StatusCode);

        // Option B: an approved agent may withdraw consent; the number is hidden but the approval stays.
        var noConsent = await agent.PutAsJsonAsync("/api/auth/me", new { fullName = "Agent Renommé", phoneNumber = "+16502530000", phonePublicConsent = false }, _ct);
        Assert.Equal(HttpStatusCode.OK, noConsent.StatusCode);
        var me = await MeAsync(agent);
        Assert.False(me.PhonePublicConsent);
        Assert.Equal(AgentStatus.Approved, me.AgentStatus);
        Assert.True(me.CanPublish);
    }

    [Fact]
    public async Task Pending_agent_must_keep_consent_until_the_request_is_decided()
    {
        var (email, _) = await _api.RegisterAsync(requestAgent: true);
        var pending = await _api.LoginAsync(email, PostgresApiFixture.UserPassword);

        var noConsent = await pending.PutAsJsonAsync("/api/auth/me", new { fullName = "En attente", phoneNumber = "+509 3712 3456", phonePublicConsent = false }, _ct);

        Assert.Equal(HttpStatusCode.BadRequest, noConsent.StatusCode);
    }

    [Fact]
    public async Task Client_can_remove_their_optional_phone()
    {
        var (client, _) = await _api.NewClientAsync();

        await client.PutAsJsonAsync("/api/auth/me", new { fullName = "Client", phoneNumber = "+509 3712 3456" }, _ct);
        var cleared = await client.PutAsJsonAsync("/api/auth/me", new { fullName = "Client", phoneNumber = "" }, _ct);

        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Null((await MeAsync(client)).PhoneNumber);
    }

    private async Task<JsonElement> LoginTokensAsync(string email)
    {
        var login = await _api.Anonymous().PostAsJsonAsync("/api/auth/login", new { email, password = PostgresApiFixture.UserPassword }, _ct);
        login.EnsureSuccessStatusCode();
        return await login.Content.ReadFromJsonAsync<JsonElement>(_ct);
    }
}
