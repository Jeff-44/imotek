using Imotek.Application.Common;
using Imotek.Application.Requests;
using Imotek.Domain.Notifications;
using Imotek.Domain.Properties;
using Imotek.Domain.Requests;
using Imotek.Infrastructure.Identity;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Application;

/// <summary>Who may send, see and answer requests, and what each side sees of the other. No database.</summary>
public class ClientRequestServiceTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid OtherAgent = Guid.NewGuid();
    private static readonly Guid Client = Guid.NewGuid();
    private static readonly Guid OtherClient = Guid.NewGuid();

    private readonly InMemoryPropertyRepository _properties = new();
    private readonly InMemoryRequestStore _store = new();
    private readonly InMemoryUserDirectory _users = new();
    private readonly FixedTimeProvider _time = new(PropertyTestData.Now);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public ClientRequestServiceTests()
    {
        _users.Add(Owner, "+50937000001", consent: true, approvedAgent: true);
        _users.Add(OtherAgent, approvedAgent: true);
        _users.Add(Client, "+50937000002");
        _users.Add(OtherClient);
    }

    private ClientRequestService As(ICurrentUser user) => new(_store, _properties, _users, user, _time, new PhoneNumberNormalizer());

    private static FakeCurrentUser ClientUser(Guid id) => new(id, Roles.Client);

    /// <summary>An approved agent keeps the Client role, so they may request other agents' properties.</summary>
    private static FakeCurrentUser AgentUser(Guid id) => new(id, Roles.Agent, Roles.Client);

    private Property Seed(TransactionType transaction = TransactionType.Rent, PropertyStatus status = PropertyStatus.Available)
    {
        var property = PropertyTestData.Property(Owner, PropertyTestData.Details(transactionType: transaction, status: status));
        _properties.Items.Add(property);
        return property;
    }

    private static CreateRequestBody Body(RequestType type, bool sharePhone = false, DateTimeOffset? visitAt = null) =>
        new() { Type = type, Message = "Je suis intéressé.", ShareClientPhone = sharePhone, VisitAt = visitAt };

    private async Task<ClientRequestResponse> SendAsync(Property property, RequestType type, bool sharePhone = false)
    {
        var visitAt = type == RequestType.Visit ? new DateTimeOffset(PropertyTestData.Now.AddDays(3)) : (DateTimeOffset?)null;
        var result = await As(ClientUser(Client)).CreateAsync(property.Id, Body(type, sharePhone, visitAt), _ct);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }

    // ---- Sending ------------------------------------------------------------

    [Fact]
    public async Task Client_sends_a_rental_request_which_notifies_the_owner()
    {
        var property = Seed();

        var sent = await SendAsync(property, RequestType.Rental);

        Assert.Equal(RequestStatus.Pending, sent.Status);
        Assert.Equal(property.Title, sent.PropertyTitle);
        Assert.Equal([RequestAction.Cancel], sent.AllowedActions);
        var notice = Assert.Single(_store.Notifications);
        Assert.Equal(Owner, notice.UserId);
        Assert.Equal(NotificationKind.RequestReceived, notice.Kind);
        Assert.Equal(RequestType.Rental, notice.RequestType);
    }

    [Fact]
    public async Task Only_signed_in_client_accounts_can_send()
    {
        var property = Seed();

        // Visitors without an account must give their contact details (see the guest tests below).
        var anonymous = await As(FakeCurrentUser.Anonymous).CreateAsync(property.Id, Body(RequestType.Rental), _ct);
        var adminOnly = await As(FakeCurrentUser.Admin(Guid.NewGuid())).CreateAsync(property.Id, Body(RequestType.Rental), _ct);
        var agent = await As(AgentUser(OtherAgent)).CreateAsync(property.Id, Body(RequestType.Rental), _ct);

        Assert.Equal(ErrorType.Validation, anonymous.Error!.Type);
        Assert.Contains("FullName", anonymous.Error.ValidationErrors!.Keys);
        Assert.Equal(ErrorType.Forbidden, adminOnly.Error!.Type);
        Assert.True(agent.IsSuccess);
    }

    [Fact]
    public async Task Owner_cannot_request_their_own_property()
    {
        var property = Seed();

        var result = await As(AgentUser(Owner)).CreateAsync(property.Id, Body(RequestType.Visit, visitAt: DateTimeOffset.UtcNow), _ct);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Empty(_store.Requests);
    }

    [Theory]
    [InlineData(PropertyStatus.Draft)]
    [InlineData(PropertyStatus.Rented)]
    [InlineData(PropertyStatus.Sold)]
    public async Task Unavailable_properties_cannot_be_requested(PropertyStatus status)
    {
        var property = Seed(status: status);

        var result = await As(ClientUser(Client)).CreateAsync(property.Id, Body(RequestType.Rental), _ct);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Request_type_must_match_the_transaction()
    {
        var forRent = Seed(TransactionType.Rent);
        var forSale = Seed(TransactionType.Sale);
        var service = As(ClientUser(Client));

        var purchaseOnRent = await service.CreateAsync(forRent.Id, Body(RequestType.Purchase), _ct);
        var rentalOnSale = await service.CreateAsync(forSale.Id, Body(RequestType.Rental), _ct);
        var noType = await service.CreateAsync(forSale.Id, new CreateRequestBody(), _ct);

        Assert.Equal(ErrorType.Validation, purchaseOnRent.Error!.Type);
        Assert.Equal(ErrorType.Validation, rentalOnSale.Error!.Type);
        Assert.Contains("Type", noType.Error!.ValidationErrors!.Keys);
        Assert.Empty(_store.Requests);
    }

    [Fact]
    public async Task Visit_needs_a_future_date()
    {
        var property = Seed();
        var service = As(ClientUser(Client));

        var missing = await service.CreateAsync(property.Id, Body(RequestType.Visit), _ct);
        var past = await service.CreateAsync(property.Id, Body(RequestType.Visit, visitAt: new DateTimeOffset(PropertyTestData.Now.AddHours(-2))), _ct);

        Assert.Contains("VisitAt", missing.Error!.ValidationErrors!.Keys);
        Assert.Contains("VisitAt", past.Error!.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task One_active_request_per_kind_but_a_new_one_once_the_previous_is_over()
    {
        var property = Seed();
        var first = await SendAsync(property, RequestType.Rental);

        var duplicate = await As(ClientUser(Client)).CreateAsync(property.Id, Body(RequestType.Rental), _ct);
        var visit = await SendAsync(property, RequestType.Visit);
        Assert.Equal(ErrorType.Conflict, duplicate.Error!.Type);
        Assert.Equal(RequestType.Visit, visit.Type);

        await As(ClientUser(Client)).ActAsync(first.Id, RequestAction.Cancel, new(), _ct);
        var again = await As(ClientUser(Client)).CreateAsync(property.Id, Body(RequestType.Rental), _ct);
        Assert.True(again.IsSuccess);
        Assert.Equal(3, _store.Requests.Count);
    }

    [Fact]
    public async Task Sharing_the_phone_needs_a_phone_on_the_profile()
    {
        var property = Seed();

        var result = await As(ClientUser(OtherClient)).CreateAsync(property.Id, Body(RequestType.Rental, sharePhone: true), _ct);

        Assert.Contains("ShareClientPhone", result.Error!.ValidationErrors!.Keys);
    }

    // ---- What each side sees -------------------------------------------------

    [Fact]
    public async Task Agent_sees_client_name_and_email_and_the_phone_only_when_shared()
    {
        var property = Seed();
        await SendAsync(property, RequestType.Rental, sharePhone: false);
        await SendAsync(property, RequestType.Visit, sharePhone: true);

        var received = (await As(AgentUser(Owner)).ListReceivedAsync(_ct)).Value;

        Assert.Equal(2, received.Count);
        var rental = received.Single(r => r.Type == RequestType.Rental);
        var visit = received.Single(r => r.Type == RequestType.Visit);
        Assert.Equal(_users.Users[Client].Email, rental.Client!.Email);
        Assert.Null(rental.Client.PhoneNumber);
        Assert.Equal("+50937000002", visit.Client!.PhoneNumber);
        Assert.Null(rental.Agent);
        Assert.Equal([RequestAction.Respond, RequestAction.Accept, RequestAction.Decline], rental.AllowedActions);
    }

    [Fact]
    public async Task Client_sees_the_agent_name_but_never_the_agent_phone_or_email()
    {
        var property = Seed();
        await SendAsync(property, RequestType.Rental);

        var mine = Assert.Single((await As(ClientUser(Client)).ListMineAsync(_ct)).Value);

        Assert.Equal(_users.Users[Owner].FullName, mine.Agent!.FullName);
        Assert.Null(mine.Agent.PhoneNumber);
        Assert.Null(mine.Agent.Email);
        Assert.Null(mine.Client);
    }

    [Fact]
    public async Task Other_agents_and_other_clients_see_nothing_and_cannot_act()
    {
        var property = Seed();
        var sent = await SendAsync(property, RequestType.Rental);

        Assert.Empty((await As(AgentUser(OtherAgent)).ListReceivedAsync(_ct)).Value);
        Assert.Empty((await As(ClientUser(OtherClient)).ListMineAsync(_ct)).Value);
        var foreignAgent = await As(AgentUser(OtherAgent)).ActAsync(sent.Id, RequestAction.Accept, new(), _ct);
        var foreignClient = await As(ClientUser(OtherClient)).ActAsync(sent.Id, RequestAction.Cancel, new(), _ct);
        // The client cannot use the agent's actions on their own request, nor the agent the client's.
        var clientAccepts = await As(ClientUser(Client)).ActAsync(sent.Id, RequestAction.Accept, new(), _ct);
        var ownerCancels = await As(AgentUser(Owner)).ActAsync(sent.Id, RequestAction.Cancel, new(), _ct);

        Assert.All([foreignAgent, foreignClient, clientAccepts, ownerCancels], r => Assert.Equal(ErrorType.NotFound, r.Error!.Type));
        Assert.Equal(RequestStatus.Pending, _store.Requests.Single().Status);
    }

    // ---- Answers ---------------------------------------------------------------

    [Fact]
    public async Task Agent_accepts_with_a_message_and_the_client_is_notified()
    {
        var property = Seed();
        var sent = await SendAsync(property, RequestType.Rental);

        var accepted = await As(AgentUser(Owner)).ActAsync(sent.Id, RequestAction.Accept, new() { Message = "Bienvenue !" }, _ct);

        Assert.Equal(RequestStatus.Accepted, accepted.Value.Status);
        Assert.Equal("Bienvenue !", accepted.Value.AgentResponse);
        Assert.Equal(PropertyStatus.Available, property.Status);
        var notice = _store.Notifications.Last();
        Assert.Equal((Client, NotificationKind.RequestAccepted), (notice.UserId, notice.Kind));
    }

    [Fact]
    public async Task Invalid_transitions_are_conflicts()
    {
        var property = Seed();
        var sent = await SendAsync(property, RequestType.Rental);
        await As(AgentUser(Owner)).ActAsync(sent.Id, RequestAction.Decline, new(), _ct);

        var acceptAfterDecline = await As(AgentUser(Owner)).ActAsync(sent.Id, RequestAction.Accept, new(), _ct);
        var proposeOnRental = await As(AgentUser(Owner)).ActAsync(sent.Id, RequestAction.ProposeDate, new(), _ct);

        Assert.Equal(ErrorType.Conflict, acceptAfterDecline.Error!.Type);
        Assert.Equal(ErrorType.Conflict, proposeOnRental.Error!.Type);
    }

    [Fact]
    public async Task Purchase_answers_never_change_the_property_status()
    {
        var property = Seed(TransactionType.Sale);
        var sent = await SendAsync(property, RequestType.Purchase);

        await As(AgentUser(Owner)).ActAsync(sent.Id, RequestAction.Respond, new() { Message = "Visite possible samedi." }, _ct);
        var closed = await As(AgentUser(Owner)).ActAsync(sent.Id, RequestAction.Close, new(), _ct);

        Assert.Equal(RequestStatus.Closed, closed.Value.Status);
        Assert.Equal(PropertyStatus.Available, property.Status);
        Assert.Equal(
            [NotificationKind.RequestReceived, NotificationKind.RequestResponded, NotificationKind.RequestClosed],
            _store.Notifications.Select(n => n.Kind));
    }

    [Fact]
    public async Task Visit_reschedule_round_trip()
    {
        var property = Seed();
        var sent = await SendAsync(property, RequestType.Visit);
        var proposed = new DateTimeOffset(PropertyTestData.Now.AddDays(6)).ToOffset(TimeSpan.FromHours(-5));

        var missingDate = await As(AgentUser(Owner)).ActAsync(sent.Id, RequestAction.ProposeDate, new(), _ct);
        var rescheduled = await As(AgentUser(Owner)).ActAsync(sent.Id, RequestAction.ProposeDate, new() { ProposedVisitAt = proposed }, _ct);
        var agreed = await As(ClientUser(Client)).ActAsync(sent.Id, RequestAction.AcceptProposal, new(), _ct);

        Assert.Contains("ProposedVisitAt", missingDate.Error!.ValidationErrors!.Keys);
        Assert.Equal(RequestStatus.Rescheduled, rescheduled.Value.Status);
        Assert.Equal(proposed.UtcDateTime, rescheduled.Value.ProposedVisitAt);
        Assert.Equal(RequestStatus.Accepted, agreed.Value.Status);
        Assert.Equal(proposed.UtcDateTime, agreed.Value.VisitAt);
        Assert.Equal(
            [NotificationKind.RequestReceived, NotificationKind.VisitRescheduled, NotificationKind.ProposalAccepted],
            _store.Notifications.Select(n => n.Kind));
        Assert.Equal(Owner, _store.Notifications.Last().UserId);
    }

    [Fact]
    public async Task Requests_stay_answerable_after_the_property_is_archived()
    {
        var property = Seed();
        var sent = await SendAsync(property, RequestType.Rental);
        property.Archive(Owner, PropertyTestData.Now);

        var declined = await As(AgentUser(Owner)).ActAsync(sent.Id, RequestAction.Decline, new(), _ct);

        Assert.Equal(RequestStatus.Declined, declined.Value.Status);
        Assert.Equal(property.Title, declined.Value.PropertyTitle);
    }
}
