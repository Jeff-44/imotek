using Imotek.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace Imotek.Tests.Infrastructure;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "imotek-storage-tests", Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorage _storage;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public LocalFileStorageTests() =>
        _storage = new LocalFileStorage(Options.Create(new FileStorageOptions { RootPath = _root, PublicBasePath = "/media/" }), Path.GetTempPath());

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Saves_reads_and_deletes_a_file()
    {
        await _storage.SaveAsync("properties/a/b.jpg", [1, 2, 3], "image/jpeg", _ct);

        var file = await _storage.OpenReadAsync("properties/a/b.jpg", _ct);
        Assert.NotNull(file);
        Assert.Equal("image/jpeg", file.ContentType);
        await using (file.Content)
        {
            Assert.Equal(3, file.Length);
        }

        await _storage.DeleteAsync("properties/a/b.jpg", _ct);
        Assert.Null(await _storage.OpenReadAsync("properties/a/b.jpg", _ct));
        Assert.False(Directory.Exists(Path.Combine(_root, "properties", "a")));
    }

    [Fact]
    public void Public_url_uses_the_configured_prefix()
    {
        Assert.Equal("/media/properties/a/b.jpg", _storage.GetPublicUrl("properties/a/b.jpg"));
    }

    [Theory]
    [InlineData("../outside.jpg")]
    [InlineData("properties/../../outside.jpg")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("properties\\..\\..\\x.jpg")]
    public async Task Keys_cannot_escape_the_storage_root(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.SaveAsync(key, [1], "image/jpeg", _ct));
        Assert.Null(await _storage.OpenReadAsync(key, _ct));
    }

    [Fact]
    public async Task Files_with_unexpected_extensions_are_not_served()
    {
        await _storage.SaveAsync("properties/a/page.html", [1], "text/html", _ct);

        Assert.Null(await _storage.OpenReadAsync("properties/a/page.html", _ct));
    }
}
