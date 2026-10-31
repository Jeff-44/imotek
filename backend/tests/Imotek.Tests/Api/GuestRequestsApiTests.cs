using System.Net;
using System.Net.Http.Json;
using Imotek.Application.Properties;
using Imotek.Application.Requests;
using Imotek.Domain.Requests;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Api;

/// <summary>Visitors without an account over HTTP with PostgreSQL: public contact, visit and rental requests, follow-up link.</summary>
[Collection(PostgresApiCollection.Name)]
public class GuestRequestsApiTests
{
    private readonly PostgresApiFixture _api;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public GuestRequestsApiTests(PostgresApiFixture api)
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

    private async Task<ClientRequestResponse> ReadAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(_ct)}");
        return (await response.Content.ReadFromJsonAsync<ClientRequestResponse>(PostgresApiFixture.Json, _ct))!;
    }

    private static string NewEmail() => $"visitor-{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Visitor_asks_for_a_visit_the_agent_proposes_a_date_and_the_visitor_accepts_it()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var propertyId = await ListingAsync(agent, "Sale");
        var visitor = _api.Anonymous();
        var email = NewEmail();
        var wanted = DateTimeOffset.UtcNow.Date.AddDays(3).AddHours(15);

        var sent = await ReadAsync(await visitor.PostAsJsonAsync($"/api/properties/{propertyId}/requests", new
        {
            type = "Visit", fullName = "Visiteur Test", email, phoneNumber = "+509 3712 3456", visitAt = new DateTimeOffset(wanted, TimeSpan.Zero),
        }, _ct));
        Assert.True(sent.IsGuest);
        Assert.NotNull(sent.TrackingToken);

        // The agent sees the visitor's contact details; nobody else can list them.
        var received = Assert.Single((await agent.GetFromJsonAsync<List<ClientRequestResponse>>("/api/requests/received", PostgresApiFixture.Json, _ct))!, r => r.Id == sent.Id);
        Assert.Equal(new RequestParty("Visiteur Test", email, "+50937123456"), received.Client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync("/api/requests/received", _ct)).StatusCode);
        var (otherAgent, _) = await _api.NewAgentAsync();
        Assert.DoesNotContain((await otherAgent.GetFromJsonAsync<List<ClientRequestResponse>>("/api/requests/received", PostgresApiFixture.Json, _ct))!, r => r.Id == sent.Id);

        var proposed = new DateTimeOffset(wanted.AddDays(1), TimeSpan.Zero);
        (await agent.PostAsJsonAsync($"/api/requests/{sent.Id}/propose-date", new { proposedVisitAt = proposed, message = "Le lendemain ?" }, _ct)).EnsureSuccessStatusCode();

        var tracked = await visitor.GetFromJsonAsync<ClientRequestResponse>($"/api/requests/track/{sent.TrackingToken}", PostgresApiFixture.Json, _ct);
        Assert.Equal(RequestStatus.Rescheduled, tracked!.Status);
        Assert.Equal("Le lendemain ?", tracked.AgentResponse);
        Assert.Null(tracked.Client);

        var accepted = await ReadAsync(await visitor.PostAsync($"/api/requests/track/{sent.TrackingToken}/accept-proposal", null, _ct));
        Assert.Equal(RequestStatus.Accepted, accepted.Status);
        Assert.Equal(proposed.UtcDateTime, accepted.VisitAt);

        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/requests/track/{GuestAccessToken.Create()}", _ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.PostAsync($"/api/requests/track/{sent.TrackingToken}/accept", null, _ct)).StatusCode);
    }

    [Fact]
    public async Task Visitor_asks_to_rent_and_the_agent_answers_accepts_or_declines()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var propertyId = await ListingAsync(agent);
        var visitor = _api.Anonymous();
        object Body(string email) => new
        {
            type = "Rental", fullName = "Locataire Test", email, phoneNumber = "+1 305 555 0142",
            durationMonths = 12, moveInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)), occupants = 2, currentAddress = "Delmas 33",
        };

        var missing = await visitor.PostAsJsonAsync($"/api/properties/{propertyId}/requests", new { type = "Rental", fullName = "X", email = NewEmail(), phoneNumber = "+50937123456" }, _ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var email = NewEmail();
        var first = await ReadAsync(await visitor.PostAsJsonAsync($"/api/properties/{propertyId}/requests", Body(email), _ct));
        Assert.Equal(HttpStatusCode.Conflict, (await visitor.PostAsJsonAsync($"/api/properties/{propertyId}/requests", Body(email.ToUpperInvariant()), _ct)).StatusCode);
        var second = await ReadAsync(await visitor.PostAsJsonAsync($"/api/properties/{propertyId}/requests", Body(NewEmail()), _ct));

        var received = (await agent.GetFromJsonAsync<List<ClientRequestResponse>>("/api/requests/received", PostgresApiFixture.Json, _ct))!;
        var shown = Assert.Single(received, r => r.Id == first.Id);
        Assert.Equal((12, 2, "Delmas 33"), (shown.DurationMonths, shown.Occupants, shown.CurrentAddress));
        Assert.Equal("+13055550142", shown.Client!.PhoneNumber);

        (await agent.PostAsJsonAsync($"/api/requests/{first.Id}/respond", new { message = "Visite possible jeudi." }, _ct)).EnsureSuccessStatusCode();
        (await agent.PostAsJsonAsync($"/api/requests/{first.Id}/accept", new { message = "Bienvenue." }, _ct)).EnsureSuccessStatusCode();
        (await agent.PostAsJsonAsync($"/api/requests/{second.Id}/decline", new { message = "Déjà loué." }, _ct)).EnsureSuccessStatusCode();

        var firstTracked = await visitor.GetFromJsonAsync<ClientRequestResponse>($"/api/requests/track/{first.TrackingToken}", PostgresApiFixture.Json, _ct);
        var secondTracked = await visitor.GetFromJsonAsync<ClientRequestResponse>($"/api/requests/track/{second.TrackingToken}", PostgresApiFixture.Json, _ct);
        Assert.Equal((RequestStatus.Accepted, "Bienvenue."), (firstTracked!.Status, firstTracked.AgentResponse));
        Assert.Equal((RequestStatus.Declined, "Déjà loué."), (secondTracked!.Status, secondTracked.AgentResponse));
    }

    [Fact]
    public async Task Public_listing_data_never_carries_visitor_details()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var propertyId = await ListingAsync(agent);
        var visitor = _api.Anonymous();
        var email = NewEmail();
        await ReadAsync(await visitor.PostAsJsonAsync($"/api/properties/{propertyId}/requests", new
        {
            type = "Visit", fullName = "Visiteur Discret", email, phoneNumber = "+509 3712 9999", visitAt = DateTimeOffset.UtcNow.AddDays(2),
        }, _ct));

        var detail = await visitor.GetStringAsync($"/api/properties/{propertyId}", _ct);
        var catalogue = await visitor.GetStringAsync("/api/properties?pageSize=50", _ct);
        var contact = await visitor.GetStringAsync($"/api/properties/{propertyId}/contact", _ct);

        foreach (var payload in new[] { detail, catalogue, contact })
        {
            Assert.DoesNotContain(email, payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("37129999", payload);
            Assert.DoesNotContain("Visiteur Discret", payload);
        }
    }
}
