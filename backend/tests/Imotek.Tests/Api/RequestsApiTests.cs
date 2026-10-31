using System.Net;
using System.Net.Http.Json;
using Imotek.Application.Auth;
using Imotek.Application.Properties;
using Imotek.Application.Requests;
using Imotek.Domain.Notifications;
using Imotek.Domain.Requests;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Api;

/// <summary>The MVP flows over HTTP with PostgreSQL: contact, rental, purchase and visit requests, notifications, archiving.</summary>
[Collection(PostgresApiCollection.Name)]
public class RequestsApiTests
{
    private readonly PostgresApiFixture _api;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public RequestsApiTests(PostgresApiFixture api)
    {
        _api = api;
        _api.SkipIfUnavailable();
    }

    private async Task<Guid> ListingAsync(HttpClient agent, string transactionType = "Rent")
    {
        var response = await agent.PostAsJsonAsync("/api/properties", PropertiesApiTests.Body(transactionType: transactionType), _ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PropertyResponse>(PostgresApiFixture.Json, _ct))!.Id;
    }

    private Task<HttpResponseMessage> SendAsync(HttpClient client, Guid propertyId, object body) =>
        client.PostAsJsonAsync($"/api/properties/{propertyId}/requests", body, _ct);

    private async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(_ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(PostgresApiFixture.Json, _ct))!;
    }

    private async Task<List<ClientRequestResponse>> ListAsync(HttpClient client, string which) =>
        (await client.GetFromJsonAsync<List<ClientRequestResponse>>($"/api/requests/{which}", PostgresApiFixture.Json, _ct))!;

    private Task<HttpResponseMessage> ActAsync(HttpClient client, Guid requestId, string verb, object? body = null) =>
        client.PostAsJsonAsync($"/api/requests/{requestId}/{verb}", body ?? new { }, _ct);

    private async Task<NotificationList> NotificationsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<NotificationList>("/api/notifications", PostgresApiFixture.Json, _ct))!;

    private async Task<UserResponse> MeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<UserResponse>("/api/auth/me", PostgresApiFixture.Json, _ct))!;

    [Fact]
    public async Task Rental_request_from_client_to_agent_and_back()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var (otherAgent, _) = await _api.NewAgentAsync();
        var (client, _) = await _api.NewClientAsync();
        var propertyId = await ListingAsync(agent);
        await client.PutAsJsonAsync("/api/auth/me", new { fullName = "Client Test", phoneNumber = "+509 3712 3456" }, _ct);

        var sent = await ReadAsync<ClientRequestResponse>(await SendAsync(client, propertyId,
            new { type = "Rental", message = "Je cherche pour janvier.", shareClientPhone = true }));
        Assert.Equal(RequestStatus.Pending, sent.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(client, propertyId, new { type = "Rental" })).StatusCode);

        // The owner sees the client and the shared phone; another agent sees nothing and cannot act.
        var received = Assert.Single(await ListAsync(agent, "received"), r => r.Id == sent.Id);
        Assert.Equal("+50937123456", received.Client!.PhoneNumber);
        Assert.Equal("Je cherche pour janvier.", received.Message);
        Assert.DoesNotContain(await ListAsync(otherAgent, "received"), r => r.Id == sent.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await ActAsync(otherAgent, sent.Id, "accept")).StatusCode);
        Assert.Contains((await NotificationsAsync(agent)).Items, n => n.RequestId == sent.Id && n.Kind == NotificationKind.RequestReceived);

        var accepted = await ReadAsync<ClientRequestResponse>(await ActAsync(agent, sent.Id, "accept", new { message = "D'accord, appelez-moi." }));
        Assert.Equal(RequestStatus.Accepted, accepted.Status);

        var mine = Assert.Single(await ListAsync(client, "mine"), r => r.Id == sent.Id);
        Assert.Equal(RequestStatus.Accepted, mine.Status);
        Assert.Equal("D'accord, appelez-moi.", mine.AgentResponse);
        Assert.Equal([RequestAction.Cancel], mine.AllowedActions);
        Assert.Null(mine.Client);

        var notices = await NotificationsAsync(client);
        Assert.Contains(notices.Items, n => n.RequestId == sent.Id && n.Kind == NotificationKind.RequestAccepted && !n.IsRead);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/notifications/read-all", null, _ct)).StatusCode);
        Assert.Equal(0, (await NotificationsAsync(client)).UnreadCount);
    }

    [Fact]
    public async Task Request_rules_are_enforced_by_the_api()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var (client, _) = await _api.NewClientAsync();
        var forRent = await ListingAsync(agent);
        var forSale = await ListingAsync(agent, "Sale");

        // Visitors: contact details required; purchases need an account.
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(_api.Anonymous(), forRent, new { type = "Visit" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(_api.Anonymous(), forSale, new { type = "Purchase" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(client, forRent, new { type = "Purchase" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(client, forSale, new { type = "Rental" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(client, forSale, new { type = "Purchase", shareClientPhone = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(agent, forSale, new { type = "Purchase" })).StatusCode);

        var purchase = await ReadAsync<ClientRequestResponse>(await SendAsync(client, forSale, new { type = "Purchase" }));
        Assert.Equal(HttpStatusCode.NotFound, (await ActAsync(agent, purchase.Id, "explode")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ActAsync(agent, purchase.Id, "accept")).StatusCode);

        var draft = await agent.PostAsJsonAsync("/api/properties", PropertiesApiTests.Body(status: "Draft"), _ct);
        var draftId = (await draft.Content.ReadFromJsonAsync<PropertyResponse>(PostgresApiFixture.Json, _ct))!.Id;
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, draftId, new { type = "Rental" })).StatusCode);
    }

    [Fact]
    public async Task Visit_request_with_a_reschedule()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var (client, _) = await _api.NewClientAsync();
        var propertyId = await ListingAsync(agent, "Sale");
        var haiti = TimeSpan.FromHours(-5);
        var wanted = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(3).AddHours(15), TimeSpan.Zero).ToOffset(haiti);

        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(client, propertyId, new { type = "Visit" })).StatusCode);
        var sent = await ReadAsync<ClientRequestResponse>(await SendAsync(client, propertyId, new { type = "Visit", visitAt = wanted }));
        Assert.Equal(wanted.UtcDateTime, sent.VisitAt);

        var proposed = wanted.AddDays(1);
        var rescheduled = await ReadAsync<ClientRequestResponse>(
            await ActAsync(agent, sent.Id, "propose-date", new { proposedVisitAt = proposed, message = "Plutôt le lendemain ?" }));
        Assert.Equal(RequestStatus.Rescheduled, rescheduled.Status);

        var agreed = await ReadAsync<ClientRequestResponse>(await ActAsync(client, sent.Id, "accept-proposal"));
        Assert.Equal(RequestStatus.Accepted, agreed.Status);
        Assert.Equal(proposed.UtcDateTime, agreed.VisitAt);
        Assert.Contains((await NotificationsAsync(agent)).Items, n => n.RequestId == sent.Id && n.Kind == NotificationKind.ProposalAccepted);
    }

    [Fact]
    public async Task Agent_phone_is_only_given_through_the_contact_endpoint_and_with_consent()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var (client, _) = await _api.NewClientAsync();
        var propertyId = await ListingAsync(agent);
        var phone = (await MeAsync(agent)).PhoneNumber!;

        var publicContact = await _api.Anonymous().GetFromJsonAsync<AgentContactResponse>($"/api/properties/{propertyId}/contact", PostgresApiFixture.Json, _ct);
        Assert.Equal(phone, publicContact!.PhoneNumber);
        var contact = await client.GetFromJsonAsync<AgentContactResponse>($"/api/properties/{propertyId}/contact", PostgresApiFixture.Json, _ct);
        Assert.Equal(phone, contact!.PhoneNumber);

        // Public property DTOs never carry the number, as a field or anywhere in the payload.
        var detail = await _api.Anonymous().GetStringAsync($"/api/properties/{propertyId}", _ct);
        var catalogue = await _api.Anonymous().GetStringAsync("/api/properties?pageSize=50", _ct);
        Assert.DoesNotContain(phone.TrimStart('+'), detail);
        Assert.DoesNotContain(phone.TrimStart('+'), catalogue);
        Assert.DoesNotContain("phone", detail, StringComparison.OrdinalIgnoreCase);

        // Withdrawing consent hides the number at once; the approval stays.
        var me = await MeAsync(agent);
        (await agent.PutAsJsonAsync("/api/auth/me", new { fullName = me.FullName, phoneNumber = phone, phonePublicConsent = false }, _ct))
            .EnsureSuccessStatusCode();
        var hidden = await client.GetFromJsonAsync<AgentContactResponse>($"/api/properties/{propertyId}/contact", PostgresApiFixture.Json, _ct);
        Assert.Null(hidden!.PhoneNumber);
        Assert.True((await MeAsync(agent)).CanPublish);
    }

    [Fact]
    public async Task Legacy_agent_listing_shows_no_number()
    {
        var (_, _, propertyId) = await _api.NewLegacyAgentAsync(withListing: true);
        var (client, _) = await _api.NewClientAsync();

        var contact = await client.GetFromJsonAsync<AgentContactResponse>($"/api/properties/{propertyId}/contact", PostgresApiFixture.Json, _ct);

        Assert.Null(contact!.PhoneNumber);
    }

    [Fact]
    public async Task Archiving_keeps_the_listing_and_its_requests()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var (client, _) = await _api.NewClientAsync();
        var propertyId = await ListingAsync(agent);
        var sent = await ReadAsync<ClientRequestResponse>(await SendAsync(client, propertyId, new { type = "Rental" }));

        Assert.Equal(HttpStatusCode.NoContent, (await agent.DeleteAsync($"/api/properties/{propertyId}", _ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await _api.Anonymous().GetAsync($"/api/properties/{propertyId}", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, propertyId, new { type = "Visit", visitAt = DateTimeOffset.UtcNow.AddDays(2) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/properties/{propertyId}/contact", _ct)).StatusCode);
        var kept = Assert.Single(await ListAsync(agent, "received"), r => r.Id == sent.Id);
        Assert.Equal(RequestStatus.Pending, kept.Status);
        Assert.Equal(HttpStatusCode.OK, (await ActAsync(agent, sent.Id, "decline", new { message = "Le bien n'est plus disponible." })).StatusCode);
    }

    [Fact]
    public async Task Concurrent_duplicates_leave_one_active_request()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var (client, _) = await _api.NewClientAsync();
        var propertyId = await ListingAsync(agent);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SendAsync(client, propertyId, new { type = "Rental" })));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Single(await ListAsync(client, "mine"));
    }
}
