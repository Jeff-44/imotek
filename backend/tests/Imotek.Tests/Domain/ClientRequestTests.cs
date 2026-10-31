using Imotek.Domain.Common;
using Imotek.Domain.Properties;
using Imotek.Domain.Requests;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Domain;

/// <summary>Creation rules and status transitions of rental, purchase and visit requests.</summary>
public class ClientRequestTests
{
    private static readonly DateTime Now = PropertyTestData.Now;
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Client = Guid.NewGuid();
    private static readonly RequestAction[] AllActions = Enum.GetValues<RequestAction>();

    private static Property ForRent(PropertyStatus status = PropertyStatus.Available) =>
        PropertyTestData.Property(Owner, PropertyTestData.Details(transactionType: TransactionType.Rent, status: status));

    private static Property ForSale() =>
        PropertyTestData.Property(Owner, PropertyTestData.Details(transactionType: TransactionType.Sale));

    private static RentalRequest Rental() => RentalRequest.Create(ForRent(), Client, "Bonjour", shareClientPhone: false, Now);

    private static PurchaseRequest Purchase() => PurchaseRequest.Create(ForSale(), Client, null, shareClientPhone: true, Now);

    private static VisitRequest Visit() => VisitRequest.Create(ForRent(), Client, null, shareClientPhone: false, Now.AddDays(2), Now);

    private static RequestAction[] Allowed(ClientRequest request) => AllActions.Where(request.Allows).ToArray();

    // ---- Creation -----------------------------------------------------------

    [Fact]
    public void A_new_request_is_pending_and_addressed_to_the_owner()
    {
        var request = Rental();

        Assert.Equal(RequestStatus.Pending, request.Status);
        Assert.Equal(Owner, request.OwnerId);
        Assert.Equal(Client, request.ClientId);
        Assert.Equal("Bonjour", request.Message);
        Assert.True(request.IsActive);
    }

    [Theory]
    [InlineData(PropertyStatus.Draft)]
    [InlineData(PropertyStatus.Rented)]
    [InlineData(PropertyStatus.Sold)]
    public void Requests_need_an_available_property(PropertyStatus status) =>
        Assert.Throws<InvalidOperationException>(() => RentalRequest.Create(ForRent(status), Client, null, false, Now));

    [Fact]
    public void Archived_property_cannot_be_requested()
    {
        var property = ForRent();
        property.Archive(Owner, Now);

        Assert.Throws<InvalidOperationException>(() => VisitRequest.Create(property, Client, null, false, Now.AddDays(1), Now));
    }

    [Fact]
    public void Rental_only_for_rent_and_purchase_only_for_sale()
    {
        Assert.Throws<DomainValidationException>(() => RentalRequest.Create(ForSale(), Client, null, false, Now));
        Assert.Throws<DomainValidationException>(() => PurchaseRequest.Create(ForRent(), Client, null, false, Now));
        Assert.Equal(RequestType.Visit, VisitRequest.Create(ForSale(), Client, null, false, Now.AddDays(1), Now).Type);
    }

    [Fact]
    public void The_owner_cannot_request_their_own_property() =>
        Assert.Throws<DomainValidationException>(() => RentalRequest.Create(ForRent(), Owner, null, false, Now));

    [Fact]
    public void Message_is_limited_and_blank_means_none()
    {
        Assert.Throws<DomainValidationException>(() =>
            RentalRequest.Create(ForRent(), Client, new string('a', ClientRequest.MessageMaxLength + 1), false, Now));
        Assert.Null(RentalRequest.Create(ForRent(), Client, "   ", false, Now).Message);
    }

    [Fact]
    public void Visit_date_must_be_in_the_future_within_the_limit_and_in_utc()
    {
        Assert.Throws<DomainValidationException>(() => VisitRequest.Create(ForRent(), Client, null, false, Now.AddMinutes(-1), Now));
        Assert.Throws<DomainValidationException>(() =>
            VisitRequest.Create(ForRent(), Client, null, false, Now + VisitRequest.MaxAdvance + TimeSpan.FromDays(1), Now));
        Assert.Throws<DomainValidationException>(() =>
            VisitRequest.Create(ForRent(), Client, null, false, DateTime.SpecifyKind(Now.AddDays(1), DateTimeKind.Unspecified), Now));
    }

    // ---- Rental -------------------------------------------------------------

    [Fact]
    public void Rental_pending_can_be_answered_accepted_declined_or_cancelled()
    {
        Assert.Equal(
            [RequestAction.Respond, RequestAction.Accept, RequestAction.Decline, RequestAction.Cancel],
            Allowed(Rental()));
    }

    [Fact]
    public void Rental_accepted_can_be_closed_by_the_agent_or_cancelled_by_the_client()
    {
        var request = Rental();
        request.Apply(RequestAction.Accept, "  Visite des clés lundi  ", Now.AddHours(1));

        Assert.Equal(RequestStatus.Accepted, request.Status);
        Assert.Equal("Visite des clés lundi", request.AgentResponse);
        Assert.Equal(Now.AddHours(1), request.UpdatedAt);
        Assert.Equal([RequestAction.Close, RequestAction.Cancel], Allowed(request));
    }

    [Fact]
    public void Responding_keeps_the_request_open_and_the_previous_answer_when_no_new_one_is_given()
    {
        var request = Rental();
        request.Apply(RequestAction.Respond, "Disponible dès décembre", Now);
        request.Apply(RequestAction.Respond, null, Now);

        Assert.Equal(RequestStatus.Responded, request.Status);
        Assert.Equal("Disponible dès décembre", request.AgentResponse);
        Assert.True(request.IsActive);
    }

    // ---- Purchase -----------------------------------------------------------

    [Fact]
    public void Purchase_has_no_accept_it_is_answered_then_declined_or_closed()
    {
        var request = Purchase();
        Assert.Equal([RequestAction.Respond, RequestAction.Decline, RequestAction.Close, RequestAction.Cancel], Allowed(request));

        request.Apply(RequestAction.Respond, "Le prix est négociable.", Now);
        Assert.Equal(RequestStatus.Responded, request.Status);

        request.Apply(RequestAction.Close, null, Now);
        Assert.Equal(RequestStatus.Closed, request.Status);
        Assert.Throws<InvalidOperationException>(() => request.Apply(RequestAction.Accept, null, Now));
    }

    // ---- Visit --------------------------------------------------------------

    [Fact]
    public void Visit_date_proposal_waits_for_the_client_then_becomes_the_visit_date()
    {
        var request = Visit();
        var proposed = Now.AddDays(5);

        request.ProposeDate(proposed, "Je ne suis pas libre ce jour-là.", Now);
        Assert.Equal(RequestStatus.Rescheduled, request.Status);
        Assert.Equal(proposed, request.ProposedVisitAt);
        Assert.Equal([RequestAction.Decline, RequestAction.ProposeDate, RequestAction.Cancel, RequestAction.AcceptProposal], Allowed(request));

        request.AcceptProposal(Now.AddHours(1));
        Assert.Equal(RequestStatus.Accepted, request.Status);
        Assert.Equal(proposed, request.VisitAt);
        Assert.Null(request.ProposedVisitAt);
    }

    [Fact]
    public void Visit_cannot_accept_a_proposal_that_does_not_exist_or_take_a_past_date()
    {
        var request = Visit();

        Assert.Throws<InvalidOperationException>(() => request.AcceptProposal(Now));
        Assert.Throws<DomainValidationException>(() => request.ProposeDate(Now.AddHours(-1), null, Now));
        Assert.Equal(RequestStatus.Pending, request.Status);
        Assert.Throws<InvalidOperationException>(() => request.Apply(RequestAction.ProposeDate, null, Now));
    }

    // ---- History ------------------------------------------------------------

    public static TheoryData<string, RequestAction> Endings() => new()
    {
        { "rental", RequestAction.Decline },
        { "rental", RequestAction.Cancel },
        { "purchase", RequestAction.Decline },
        { "purchase", RequestAction.Close },
        { "purchase", RequestAction.Cancel },
        { "visit", RequestAction.Decline },
        { "visit", RequestAction.Cancel },
    };

    [Theory]
    [MemberData(nameof(Endings))]
    public void Ended_requests_are_kept_and_allow_nothing_more(string kind, RequestAction ending)
    {
        ClientRequest request = kind switch { "rental" => Rental(), "purchase" => Purchase(), _ => Visit() };

        request.Apply(ending, null, Now);

        Assert.False(request.IsActive);
        Assert.Empty(Allowed(request));
    }
}
