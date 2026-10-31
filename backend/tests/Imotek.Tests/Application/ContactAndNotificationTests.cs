using Imotek.Application.Common;
using Imotek.Application.Requests;
using Imotek.Domain.Notifications;
using Imotek.Domain.Properties;
using Imotek.Domain.Requests;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Application;

/// <summary>When an agent's number may be shown, and who reads which notifications.</summary>
public class ContactAndNotificationTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Visitor = Guid.NewGuid();

    private readonly InMemoryPropertyRepository _properties = new();
    private readonly InMemoryUserDirectory _users = new();
    private readonly InMemoryRequestStore _store = new();
    private readonly FixedTimeProvider _time = new(PropertyTestData.Now);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private AgentContactService Contacts(ICurrentUser user) => new(_properties, _users, user);

    private NotificationService Notifications(Guid userId) => new(_store, FakeCurrentUser.Client(userId), _time);

    private Property Seed(PropertyStatus status = PropertyStatus.Available)
    {
        var property = PropertyTestData.Property(Owner, PropertyTestData.Details(status: status));
        _properties.Items.Add(property);
        return property;
    }

    // ---- Contact ----------------------------------------------------------------

    [Fact]
    public async Task Approved_agent_who_consents_shows_their_number_to_signed_in_users()
    {
        _users.Add(Owner, "+50937000001", consent: true, approvedAgent: true);
        var property = Seed();

        var contact = await Contacts(FakeCurrentUser.Client(Visitor)).GetAsync(property.Id, _ct);

        Assert.Equal("+50937000001", contact.Value.PhoneNumber);
        Assert.Equal(_users.Users[Owner].FullName, contact.Value.AgentName);
    }

    [Fact]
    public async Task Anonymous_visitors_get_nothing()
    {
        _users.Add(Owner, "+50937000001", consent: true, approvedAgent: true);

        var contact = await Contacts(FakeCurrentUser.Anonymous).GetAsync(Seed().Id, _ct);

        Assert.Equal(ErrorType.Unauthorized, contact.Error!.Type);
    }

    [Theory]
    [InlineData("+50937000001", false, true)] // no consent
    [InlineData("+50937000001", true, false)] // not an approved agent (legacy or pending)
    [InlineData(null, true, true)] // no number
    public async Task Number_is_hidden_unless_every_condition_holds(string? phone, bool consent, bool approved)
    {
        _users.Add(Owner, phone, consent, approved);

        var contact = await Contacts(FakeCurrentUser.Client(Visitor)).GetAsync(Seed().Id, _ct);

        Assert.True(contact.IsSuccess);
        Assert.Null(contact.Value.PhoneNumber);
    }

    [Fact]
    public async Task Withdrawing_consent_hides_the_number_at_once()
    {
        _users.Add(Owner, "+50937000001", consent: true, approvedAgent: true);
        var property = Seed();
        var service = Contacts(FakeCurrentUser.Client(Visitor));
        Assert.NotNull((await service.GetAsync(property.Id, _ct)).Value.PhoneNumber);

        _users.Add(Owner, "+50937000001", consent: false, approvedAgent: true);

        Assert.Null((await service.GetAsync(property.Id, _ct)).Value.PhoneNumber);
    }

    [Theory]
    [InlineData(PropertyStatus.Draft)]
    [InlineData(PropertyStatus.Sold)]
    public async Task Unavailable_listing_gives_no_contact(PropertyStatus status)
    {
        _users.Add(Owner, "+50937000001", consent: true, approvedAgent: true);

        var contact = await Contacts(FakeCurrentUser.Client(Visitor)).GetAsync(Seed(status).Id, _ct);

        Assert.Equal(ErrorType.NotFound, contact.Error!.Type);
    }

    // ---- Notifications -------------------------------------------------------------

    private Notification Notify(Guid userId)
    {
        var request = RentalRequest.Create(Seed(), Visitor, null, false, PropertyTestData.Now);
        var notification = Notification.About(request, userId, NotificationKind.RequestReceived, "Maison", PropertyTestData.Now);
        _store.Notifications.Add(notification);
        return notification;
    }

    [Fact]
    public async Task Users_list_and_mark_only_their_own_notifications()
    {
        var mine = Notify(Owner);
        var theirs = Notify(Visitor);

        var list = (await Notifications(Owner).ListAsync(_ct)).Value;
        var foreign = await Notifications(Owner).MarkReadAsync(theirs.Id, _ct);
        var own = await Notifications(Owner).MarkReadAsync(mine.Id, _ct);

        Assert.Equal(mine.Id, Assert.Single(list.Items).Id);
        Assert.Equal(1, list.UnreadCount);
        Assert.Equal(ErrorType.NotFound, foreign.Error!.Type);
        Assert.True(own.IsSuccess);
        Assert.True(mine.IsRead);
        Assert.False(theirs.IsRead);
    }

    [Fact]
    public async Task Mark_all_read_leaves_other_users_alone()
    {
        Notify(Owner);
        Notify(Owner);
        var theirs = Notify(Visitor);

        await Notifications(Owner).MarkAllReadAsync(_ct);

        Assert.Equal(0, (await Notifications(Owner).ListAsync(_ct)).Value.UnreadCount);
        Assert.False(theirs.IsRead);
    }
}
