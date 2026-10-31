using Imotek.Application.Common;
using Imotek.Application.Requests;
using Imotek.Domain.Notifications;
using Imotek.Domain.Properties;
using Imotek.Domain.Requests;
using Imotek.Infrastructure.Identity;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Application;

/// <summary>Visit and rental requests from visitors without an account, and their private follow-up link.</summary>
public class GuestRequestTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid OtherAgent = Guid.NewGuid();

    private readonly InMemoryPropertyRepository _properties = new();
    private readonly InMemoryRequestStore _store = new();
    private readonly InMemoryUserDirectory _users = new();
    private readonly FixedTimeProvider _time = new(PropertyTestData.Now);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public GuestRequestTests()
    {
        _users.Add(Owner, "+50937000001", consent: true, approvedAgent: true);
        _users.Add(OtherAgent, approvedAgent: true);
    }

    private ClientRequestService Visitor => As(FakeCurrentUser.Anonymous);

    private ClientRequestService Agent(Guid id) => As(new FakeCurrentUser(id, Roles.Agent, Roles.Client));

    private ClientRequestService As(ICurrentUser user) => new(_store, _properties, _users, user, _time, new PhoneNumberNormalizer());

    private Property Seed(TransactionType transaction = TransactionType.Rent, PropertyStatus status = PropertyStatus.Available)
    {
        var property = PropertyTestData.Property(Owner, PropertyTestData.Details(transactionType: transaction, status: status));
        _properties.Items.Add(property);
        return property;
    }

    private static DateTimeOffset InDays(int days) => new(PropertyTestData.Now.AddDays(days));

    private static CreateRequestBody Visit(string email = "Marie@Example.com") => new()
    {
        Type = RequestType.Visit,
        FullName = "  Marie Joseph ",
        Email = email,
        PhoneNumber = "+509 3712 3456",
        VisitAt = InDays(2),
        Message = "Samedi matin ?",
    };

    private static CreateRequestBody Rental(string email = "jean@example.com") => new()
    {
        Type = RequestType.Rental,
        FullName = "Jean Pierre",
        Email = email,
        PhoneNumber = "3712 3456",
        DurationMonths = 12,
        MoveInDate = DateOnly.FromDateTime(PropertyTestData.Now.AddDays(30)),
        Occupants = 3,
        CurrentAddress = "Delmas 33",
    };

    [Fact]
    public async Task Visitor_sends_a_visit_request_without_an_account()
    {
        var property = Seed();

        var sent = (await Visitor.CreateAsync(property.Id, Visit(), _ct)).Value;

        var stored = Assert.Single(_store.Requests);
        Assert.Null(stored.ClientId);
        Assert.True(stored.IsGuest);
        Assert.Equal(("Marie Joseph", "marie@example.com", "+50937123456"), (stored.GuestName, stored.GuestEmail, stored.GuestPhone));
        Assert.Equal(Owner, stored.OwnerId);
        Assert.True(GuestAccessToken.IsWellFormed(sent.TrackingToken));
        Assert.Equal(GuestAccessToken.Hash(sent.TrackingToken!), stored.GuestAccessTokenHash);
        Assert.NotEqual(sent.TrackingToken, stored.GuestAccessTokenHash);
        Assert.Null(sent.Client);
        var notice = Assert.Single(_store.Notifications);
        Assert.Equal((Owner, NotificationKind.RequestReceived), (notice.UserId, notice.Kind));
    }

    [Fact]
    public async Task The_agent_sees_everything_the_visitor_typed()
    {
        var property = Seed();
        await Visitor.CreateAsync(property.Id, Rental(), _ct);

        var received = Assert.Single((await Agent(Owner).ListReceivedAsync(_ct)).Value);

        Assert.True(received.IsGuest);
        Assert.Equal(new RequestParty("Jean Pierre", "jean@example.com", "+50937123456"), received.Client);
        Assert.Equal((12, 3, "Delmas 33"), (received.DurationMonths, received.Occupants, received.CurrentAddress));
        Assert.Equal(DateOnly.FromDateTime(PropertyTestData.Now.AddDays(30)), received.MoveInDate);
        Assert.Equal([RequestAction.Respond, RequestAction.Accept, RequestAction.Decline], received.AllowedActions);
        Assert.Null(received.TrackingToken);
        Assert.Empty((await Agent(OtherAgent).ListReceivedAsync(_ct)).Value);
    }

    [Fact]
    public async Task Visitor_contact_details_are_required_and_validated()
    {
        var property = Seed();

        var empty = await Visitor.CreateAsync(property.Id, new CreateRequestBody { Type = RequestType.Visit, VisitAt = InDays(1) }, _ct);
        var invalid = await Visitor.CreateAsync(property.Id, Visit() with { Email = "marie@", PhoneNumber = "12" }, _ct);

        Assert.Equal(["Email", "FullName", "PhoneNumber"], empty.Error!.ValidationErrors!.Keys.Order());
        Assert.Equal(["Email", "PhoneNumber"], invalid.Error!.ValidationErrors!.Keys.Order());
        Assert.Empty(_store.Requests);
    }

    [Fact]
    public async Task Visitor_rental_needs_the_duration_and_the_move_in_date()
    {
        var property = Seed();

        var missing = await Visitor.CreateAsync(property.Id, Rental() with { DurationMonths = null, MoveInDate = null }, _ct);
        var past = await Visitor.CreateAsync(property.Id, Rental() with { MoveInDate = DateOnly.FromDateTime(PropertyTestData.Now.AddDays(-1)) }, _ct);

        Assert.Contains("DurationMonths", missing.Error!.ValidationErrors!.Keys);
        Assert.Contains("MoveInDate", missing.Error.ValidationErrors.Keys);
        Assert.Contains("MoveInDate", past.Error!.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Visitors_cannot_send_purchases_or_requests_on_unavailable_or_wrong_listings()
    {
        var forSale = Seed(TransactionType.Sale);
        var sold = Seed(TransactionType.Sale, PropertyStatus.Sold);

        var purchase = await Visitor.CreateAsync(forSale.Id, Visit() with { Type = RequestType.Purchase }, _ct);
        var rentalOnSale = await Visitor.CreateAsync(forSale.Id, Rental(), _ct);
        var unavailable = await Visitor.CreateAsync(sold.Id, Visit(), _ct);
        var archived = Seed();
        archived.Archive(Owner, PropertyTestData.Now);
        var onArchived = await Visitor.CreateAsync(archived.Id, Visit(), _ct);

        Assert.Equal(ErrorType.Unauthorized, purchase.Error!.Type);
        Assert.Contains("Type", rentalOnSale.Error!.ValidationErrors!.Keys);
        Assert.Equal(ErrorType.NotFound, unavailable.Error!.Type);
        Assert.Equal(ErrorType.NotFound, onArchived.Error!.Type);
        Assert.Empty(_store.Requests);
    }

    [Fact]
    public async Task One_open_request_per_visitor_email_but_a_new_need_is_not_blocked()
    {
        var property = Seed();
        var first = (await Visitor.CreateAsync(property.Id, Visit(), _ct)).Value;

        var sameEmail = await Visitor.CreateAsync(property.Id, Visit(email: "MARIE@example.com"), _ct);
        var otherKind = await Visitor.CreateAsync(property.Id, Rental(email: "marie@example.com"), _ct);
        var otherPerson = await Visitor.CreateAsync(property.Id, Visit(email: "paul@example.com"), _ct);
        await Visitor.ActAsGuestAsync(first.TrackingToken!, RequestAction.Cancel, _ct);
        var again = await Visitor.CreateAsync(property.Id, Visit(), _ct);

        Assert.Equal(ErrorType.Conflict, sameEmail.Error!.Type);
        Assert.True(otherKind.IsSuccess);
        Assert.True(otherPerson.IsSuccess);
        Assert.True(again.IsSuccess);
    }

    [Fact]
    public async Task The_private_link_shows_the_answer_and_lets_the_visitor_accept_a_new_date()
    {
        var property = Seed();
        var sent = (await Visitor.CreateAsync(property.Id, Visit(), _ct)).Value;
        var proposed = InDays(5);
        await Agent(Owner).ActAsync(sent.Id, RequestAction.ProposeDate, new() { ProposedVisitAt = proposed, Message = "Plutôt mardi ?" }, _ct);

        var tracked = (await Visitor.TrackAsync(sent.TrackingToken!, _ct)).Value;
        var accepted = (await Visitor.ActAsGuestAsync(sent.TrackingToken!, RequestAction.AcceptProposal, _ct)).Value;

        Assert.Equal(RequestStatus.Rescheduled, tracked.Status);
        Assert.Equal("Plutôt mardi ?", tracked.AgentResponse);
        Assert.Equal(_users.Users[Owner].FullName, tracked.Agent!.FullName);
        Assert.Null(tracked.Client);
        Assert.Equal([RequestAction.Cancel, RequestAction.AcceptProposal], tracked.AllowedActions);
        Assert.Equal(RequestStatus.Accepted, accepted.Status);
        Assert.Equal(proposed.UtcDateTime, accepted.VisitAt);
        // The agent is notified of the visitor's answer; the visitor, who has no account, is not.
        Assert.Equal(
            [(Owner, NotificationKind.RequestReceived), (Owner, NotificationKind.ProposalAccepted)],
            _store.Notifications.Select(n => (n.UserId, n.Kind)));
    }

    [Fact]
    public async Task A_wrong_or_malformed_link_finds_nothing_and_agent_actions_are_not_open_to_it()
    {
        var property = Seed();
        var sent = (await Visitor.CreateAsync(property.Id, Visit(), _ct)).Value;

        var wrong = await Visitor.TrackAsync(GuestAccessToken.Create(), _ct);
        var malformed = await Visitor.TrackAsync("not-a-token", _ct);
        var agentAction = await Visitor.ActAsGuestAsync(sent.TrackingToken!, RequestAction.Accept, _ct);
        var hashAsToken = await Visitor.TrackAsync(_store.Requests.Single().GuestAccessTokenHash!, _ct);

        Assert.All([wrong, malformed, agentAction, hashAsToken], r => Assert.Equal(ErrorType.NotFound, r.Error!.Type));
        Assert.Equal(RequestStatus.Pending, _store.Requests.Single().Status);
    }

    [Fact]
    public async Task Signed_in_flows_are_unchanged_and_never_get_a_link()
    {
        var property = Seed();
        var clientId = Guid.NewGuid();
        _users.Add(clientId);

        var sent = await As(FakeCurrentUser.Client(clientId)).CreateAsync(property.Id, new CreateRequestBody { Type = RequestType.Rental }, _ct);

        Assert.True(sent.IsSuccess);
        Assert.Null(sent.Value.TrackingToken);
        Assert.Null(_store.Requests.Single().GuestAccessTokenHash);
        Assert.Equal(clientId, _store.Requests.Single().ClientId);
    }
}
