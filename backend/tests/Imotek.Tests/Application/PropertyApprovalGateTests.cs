using Imotek.Application.Common;
using Imotek.Application.Images;
using Imotek.Application.Properties;
using Imotek.Domain.Properties;
using Imotek.Infrastructure.Images;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Application;

/// <summary>
/// Agent approval gate on listings (decision C1): unapproved agents — pending or legacy — cannot
/// create or change content, but can still view and remove what they own. Admins are not gated.
/// </summary>
public class PropertyApprovalGateTests
{
    private static readonly Guid Unapproved = Guid.NewGuid();

    private readonly InMemoryPropertyRepository _repository = new();
    private readonly InMemoryFileStorage _storage = new();
    private readonly FakeAgentAccess _access = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public PropertyApprovalGateTests() => _access.NotApproved.Add(Unapproved);

    private PropertyService ServiceFor(ICurrentUser user) =>
        new(_repository, user, new FixedTimeProvider(PropertyTestData.Now), _storage, new ImageProcessor(), _access);

    private Property SeedOwnedByUnapproved()
    {
        var property = PropertyTestData.Property(Unapproved);
        _repository.Items.Add(property);
        return property;
    }

    private static UploadedFile Jpeg() => new(new MemoryStream(TestImages.JpegWithExif()), TestImages.JpegWithExif().Length, "a.jpg", "image/jpeg");

    [Fact]
    public async Task Unapproved_agent_cannot_create()
    {
        var result = await ServiceFor(FakeCurrentUser.Agent(Unapproved)).CreateAsync(PropertyTestData.Request(), _ct);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Equal(PropertyService.ApprovalRequiredMessage, result.Error.Message);
        Assert.Empty(_repository.Items);
    }

    [Fact]
    public async Task Unapproved_agent_cannot_edit_own_listing()
    {
        var property = SeedOwnedByUnapproved();

        var result = await ServiceFor(FakeCurrentUser.Agent(Unapproved))
            .UpdateAsync(property.Id, PropertyTestData.Request(title: "Nouveau titre", price: 1m), _ct);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.NotEqual("Nouveau titre", property.Title);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Unapproved_agent_cannot_add_or_remove_photos()
    {
        var property = SeedOwnedByUnapproved();
        var image = PropertyImage.Create(Guid.NewGuid(), "properties/k/i.jpg", "image/jpeg", 10, 0, PropertyTestData.Now);
        property.AddImage(image, PropertyTestData.Now);
        var service = ServiceFor(FakeCurrentUser.Agent(Unapproved));

        var add = await service.AddImageAsync(property.Id, Jpeg(), _ct);
        var remove = await service.DeleteImageAsync(property.Id, image.Id, _ct);

        Assert.Equal(ErrorType.Forbidden, add.Error!.Type);
        Assert.Equal(ErrorType.Forbidden, remove.Error!.Type);
        Assert.Single(property.Images);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Unapproved_agent_can_still_view_and_remove_own_listing()
    {
        var property = SeedOwnedByUnapproved();
        var service = ServiceFor(FakeCurrentUser.Agent(Unapproved));

        var mine = await service.SearchMineAsync(new PropertySearchRequest(), _ct);
        var deleted = await service.DeleteAsync(property.Id, _ct);

        Assert.Equal(property.Id, Assert.Single(mine.Value.Items).Id);
        Assert.True(deleted.IsSuccess);
    }

    [Fact]
    public async Task Unapproved_agent_still_cannot_touch_someone_elses_listing()
    {
        var other = PropertyTestData.Property(Guid.NewGuid());
        _repository.Items.Add(other);

        var result = await ServiceFor(FakeCurrentUser.Agent(Unapproved)).DeleteAsync(other.Id, _ct);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Contains(other, _repository.Items);
    }

    [Fact]
    public async Task Admin_is_not_gated_by_agent_approval()
    {
        var property = SeedOwnedByUnapproved();
        var admin = new FakeAgentAccess(approvedByDefault: false); // the admin is not an approved agent

        var result = await new PropertyService(_repository, FakeCurrentUser.Admin(Guid.NewGuid()), new FixedTimeProvider(PropertyTestData.Now), _storage, new ImageProcessor(), admin)
            .UpdateAsync(property.Id, PropertyTestData.Request(title: "Corrigé par l'admin"), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("Corrigé par l'admin", property.Title);
    }
}
