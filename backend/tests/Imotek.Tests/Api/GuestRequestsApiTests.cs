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

    /// <summary>Follow-up calls as the page makes them: the token in a header, never in the URL.</summary>
    private Task<HttpResponseMessage> TrackAsync(HttpClient client, string? token) =>
        client.SendAsync(WithToken(new HttpRequestMessage(HttpMethod.Get, "/api/requests/track"), token), _ct);

    private Task<HttpResponseMessage> ActAsync(HttpClient client, string? token, string verb) =>
        client.SendAsync(WithToken(new HttpRequestMessage(HttpMethod.Post, $"/api/requests/track/{verb}"), token), _ct);

    private static HttpRequestMessage WithToken(HttpRequestMessage request, string? token)
    {
        if (token is not null)
        {
            request.Headers.Add("X-Tracking-Token", token);
        }

        return request;
    }

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

        var tracked = await ReadAsync(await TrackAsync(visitor, sent.TrackingToken));
        Assert.Equal(RequestStatus.Rescheduled, tracked.Status);
        Assert.Equal("Le lendemain ?", tracked.AgentResponse);
        Assert.Null(tracked.Client);

        var accepted = await ReadAsync(await ActAsync(visitor, sent.TrackingToken, "accept-proposal"));
        Assert.Equal(RequestStatus.Accepted, accepted.Status);
        Assert.Equal(proposed.UtcDateTime, accepted.VisitAt);

        Assert.Equal(HttpStatusCode.NotFound, (await TrackAsync(visitor, GuestAccessToken.Create())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ActAsync(visitor, sent.TrackingToken, "accept")).StatusCode);
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

        var firstTracked = await ReadAsync(await TrackAsync(visitor, first.TrackingToken));
        var secondTracked = await ReadAsync(await TrackAsync(visitor, second.TrackingToken));
        Assert.Equal((RequestStatus.Accepted, "Bienvenue."), (firstTracked.Status, firstTracked.AgentResponse));
        Assert.Equal((RequestStatus.Declined, "Déjà loué."), (secondTracked.Status, secondTracked.AgentResponse));
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

    [Fact]
    public async Task Invalid_missing_or_foreign_tokens_all_get_the_same_answer()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var propertyId = await ListingAsync(agent);
        var visitor = _api.Anonymous();
        var mine = await ReadAsync(await visitor.PostAsJsonAsync($"/api/properties/{propertyId}/requests", new
        {
            type = "Visit", fullName = "Visiteur A", email = NewEmail(), phoneNumber = "+509 3712 3456", visitAt = DateTimeOffset.UtcNow.AddDays(2),
        }, _ct));
        var other = await ReadAsync(await visitor.PostAsJsonAsync($"/api/properties/{propertyId}/requests", new
        {
            type = "Visit", fullName = "Visiteur B", email = NewEmail(), phoneNumber = "+509 3712 3457", visitAt = DateTimeOffset.UtcNow.AddDays(3),
        }, _ct));

        var answers = new[]
        {
            await TrackAsync(visitor, null),
            await TrackAsync(visitor, ""),
            await TrackAsync(visitor, "not-a-token"),
            await TrackAsync(visitor, GuestAccessToken.Create()),
            await visitor.GetAsync($"/api/requests/track/{GuestAccessToken.Create()}", _ct),
            await ActAsync(visitor, null, "cancel"),
            await ActAsync(visitor, GuestAccessToken.Create(), "cancel"),
        };
        var bodies = await Task.WhenAll(answers.Select(a => a.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(_ct)));

        Assert.All(answers, a => Assert.Equal(HttpStatusCode.NotFound, a.StatusCode));
        Assert.All(bodies, b => Assert.Equal("The request was not found.", b.GetProperty("title").GetString()));

        // One visitor's token opens their own request only.
        var seen = await ReadAsync(await TrackAsync(visitor, mine.TrackingToken));
        Assert.Equal(mine.Id, seen.Id);
        Assert.NotEqual(other.Id, seen.Id);
        Assert.Equal(RequestStatus.Pending, (await ReadAsync(await TrackAsync(visitor, other.TrackingToken))).Status);
        await ReadAsync(await ActAsync(visitor, mine.TrackingToken, "cancel"));
        Assert.Equal(RequestStatus.Pending, (await ReadAsync(await TrackAsync(visitor, other.TrackingToken))).Status);
    }

    [Fact]
    public async Task Old_path_links_keep_working_during_the_transition()
    {
        var (agent, _) = await _api.NewAgentAsync();
        var propertyId = await ListingAsync(agent);
        var visitor = _api.Anonymous();
        var sent = await ReadAsync(await visitor.PostAsJsonAsync($"/api/properties/{propertyId}/requests", new
        {
            type = "Visit", fullName = "Ancien Lien", email = NewEmail(), phoneNumber = "+509 3712 3456", visitAt = DateTimeOffset.UtcNow.AddDays(2),
        }, _ct));

        var tracked = await visitor.GetFromJsonAsync<ClientRequestResponse>($"/api/requests/track/{sent.TrackingToken}", PostgresApiFixture.Json, _ct);
        var cancelled = await ReadAsync(await visitor.PostAsync($"/api/requests/track/{sent.TrackingToken}/cancel", null, _ct));

        Assert.Equal(sent.Id, tracked!.Id);
        Assert.Equal(RequestStatus.Cancelled, cancelled.Status);
        Assert.Equal(RequestStatus.Cancelled, (await ReadAsync(await TrackAsync(visitor, sent.TrackingToken))).Status);
    }
}
