using Imotek.Application.Common;
using Imotek.Application.Images;
using Imotek.Application.Properties;
using Imotek.Domain.Properties;
using Imotek.Infrastructure.Images;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Application;

/// <summary>Photo upload rules of <see cref="PropertyService"/>, with in-memory storage.</summary>
public class PropertyImageTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid OtherAgent = Guid.NewGuid();

    private readonly InMemoryPropertyRepository _repository = new();
    private readonly InMemoryFileStorage _storage = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private PropertyService ServiceFor(ICurrentUser user) =>
        new(_repository, user, new FixedTimeProvider(PropertyTestData.Now), _storage, new ImageProcessor());

    private Property Seed(PropertyStatus status = PropertyStatus.Available, int links = 0)
    {
        var urls = Enumerable.Range(0, links).Select(i => $"https://example.com/{i}.jpg").ToList();
        var property = PropertyTestData.Property(Owner, PropertyTestData.Details(status: status, imageUrls: urls));
        _repository.Items.Add(property);
        return property;
    }

    private static UploadedFile File(byte[] content, string name = "photo.jpg", string type = "image/jpeg", long? length = null) =>
        new(new MemoryStream(content), length ?? content.Length, name, type);

    [Fact]
    public async Task Owner_uploads_a_photo_that_is_stored_without_metadata()
    {
        var property = Seed();

        var result = await ServiceFor(FakeCurrentUser.Agent(Owner)).AddImageAsync(property.Id, File(TestImages.JpegWithExif()), _ct);

        Assert.True(result.IsSuccess);
        var photo = Assert.Single(result.Value.Photos);
        var image = Assert.Single(property.Images);
        Assert.Equal(image.Id, photo.Id);
        Assert.Equal($"/media/{image.StorageKey}", photo.Url);
        Assert.Equal(photo.Url, result.Value.ImageUrls[0]);
        Assert.Matches("^properties/[0-9a-f]{32}/[0-9a-f]{32}\\.jpg$", image.StorageKey);
        Assert.False(TestImages.Contains(_storage.Files[image.StorageKey], TestImages.GpsMarker));
    }

    [Fact]
    public async Task Declared_name_and_type_are_ignored()
    {
        var property = Seed();

        // A PNG sent as "evil.html" with a text/html content type is stored as a PNG with a generated name.
        var result = await ServiceFor(FakeCurrentUser.Agent(Owner))
            .AddImageAsync(property.Id, File(TestImages.PngWithText(), "evil.html", "text/html"), _ct);

        Assert.True(result.IsSuccess);
        var image = Assert.Single(property.Images);
        Assert.Equal("image/png", image.ContentType);
        Assert.EndsWith(".png", image.StorageKey);
        Assert.DoesNotContain("evil", image.StorageKey);
    }

    [Fact]
    public async Task Non_image_content_is_rejected_even_with_an_image_name()
    {
        var property = Seed();

        var result = await ServiceFor(FakeCurrentUser.Agent(Owner)).AddImageAsync(property.Id, File(TestImages.Svg(), "photo.jpg"), _ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Empty(property.Images);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Files_over_the_size_limit_are_rejected_without_being_read()
    {
        var property = Seed();

        var result = await ServiceFor(FakeCurrentUser.Agent(Owner))
            .AddImageAsync(property.Id, File(TestImages.JpegWithExif(), length: ImageRules.MaxUploadBytes + 1), _ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task A_stream_longer_than_its_declared_length_is_rejected()
    {
        var property = Seed();
        var oversized = new byte[ImageRules.MaxUploadBytes + 10];
        TestImages.JpegWithExif().CopyTo(oversized, 0);

        var result = await ServiceFor(FakeCurrentUser.Agent(Owner)).AddImageAsync(property.Id, File(oversized, length: 100), _ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Photo_limit_includes_external_links()
    {
        var property = Seed(links: Property.MaxImages);

        var result = await ServiceFor(FakeCurrentUser.Agent(Owner)).AddImageAsync(property.Id, File(TestImages.JpegWithExif()), _ct);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Theory]
    [InlineData(PropertyStatus.Available, ErrorType.Forbidden)]
    [InlineData(PropertyStatus.Draft, ErrorType.NotFound)]
    public async Task Other_agents_cannot_upload(PropertyStatus status, ErrorType expected)
    {
        var property = Seed(status);

        var result = await ServiceFor(FakeCurrentUser.Agent(OtherAgent)).AddImageAsync(property.Id, File(TestImages.JpegWithExif()), _ct);

        Assert.Equal(expected, result.Error!.Type);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Clients_cannot_upload()
    {
        var property = Seed();

        var result = await ServiceFor(FakeCurrentUser.Client(Guid.NewGuid())).AddImageAsync(property.Id, File(TestImages.JpegWithExif()), _ct);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
    }

    [Fact]
    public async Task Admin_can_manage_photos_of_any_property()
    {
        var property = Seed(PropertyStatus.Draft);
        var admin = ServiceFor(FakeCurrentUser.Admin(Guid.NewGuid()));

        var added = await admin.AddImageAsync(property.Id, File(TestImages.WebPWithExif(), "x.webp", "image/webp"), _ct);
        var removed = await admin.DeleteImageAsync(property.Id, added.Value.Photos[0].Id, _ct);

        Assert.True(removed.IsSuccess);
        Assert.Empty(property.Images);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Deleting_a_photo_removes_the_file_and_unknown_ids_are_not_found()
    {
        var property = Seed();
        var service = ServiceFor(FakeCurrentUser.Agent(Owner));
        var added = await service.AddImageAsync(property.Id, File(TestImages.JpegWithExif()), _ct);

        Assert.Equal(ErrorType.NotFound, (await service.DeleteImageAsync(property.Id, Guid.NewGuid(), _ct)).Error!.Type);
        Assert.True((await service.DeleteImageAsync(property.Id, added.Value.Photos[0].Id, _ct)).IsSuccess);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Other_agents_cannot_delete_photos()
    {
        var property = Seed();
        var added = await ServiceFor(FakeCurrentUser.Agent(Owner)).AddImageAsync(property.Id, File(TestImages.JpegWithExif()), _ct);

        var result = await ServiceFor(FakeCurrentUser.Agent(OtherAgent)).DeleteImageAsync(property.Id, added.Value.Photos[0].Id, _ct);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Single(_storage.Files);
    }

    [Fact]
    public async Task Deleting_a_property_deletes_its_photo_files()
    {
        var property = Seed();
        var service = ServiceFor(FakeCurrentUser.Agent(Owner));
        await service.AddImageAsync(property.Id, File(TestImages.JpegWithExif()), _ct);
        await service.AddImageAsync(property.Id, File(TestImages.PngWithText()), _ct);
        Assert.Equal(2, _storage.Files.Count);

        Assert.True((await service.DeleteAsync(property.Id, _ct)).IsSuccess);

        Assert.Empty(_storage.Files);
    }
}
