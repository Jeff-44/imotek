using Imotek.Infrastructure.Identity;
using Imotek.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace Imotek.Tests.Infrastructure;

public class SecurityStampCacheTests
{
    private readonly FixedTimeProvider _time = new(PropertyTestData.Now);

    private SecurityStampCache Cache(int seconds = 30) => new(_time, Options.Create(new SecurityStampCacheOptions { Seconds = seconds }));

    [Fact]
    public void Keeps_a_stamp_for_its_lifetime_only()
    {
        var cache = Cache();
        var user = Guid.NewGuid();
        cache.Set(user, "stamp-1");

        _time.UtcNow = PropertyTestData.Now.AddSeconds(29);
        Assert.True(cache.TryGet(user, out var stamp));
        Assert.Equal("stamp-1", stamp);

        _time.UtcNow = PropertyTestData.Now.AddSeconds(30);
        Assert.False(cache.TryGet(user, out _));
    }

    [Fact]
    public void Invalidation_drops_the_stamp_at_once_and_only_for_that_user()
    {
        var cache = Cache();
        var (user, other) = (Guid.NewGuid(), Guid.NewGuid());
        cache.Set(user, "old");
        cache.Set(other, "theirs");

        cache.Invalidate(user);

        Assert.False(cache.TryGet(user, out _));
        Assert.True(cache.TryGet(other, out var kept));
        Assert.Equal("theirs", kept);
    }

    [Fact]
    public void A_new_value_replaces_the_old_one_and_restarts_the_lifetime()
    {
        var cache = Cache(seconds: 10);
        var user = Guid.NewGuid();
        cache.Set(user, "old");
        _time.UtcNow = PropertyTestData.Now.AddSeconds(8);

        cache.Set(user, "new");
        _time.UtcNow = PropertyTestData.Now.AddSeconds(15);

        Assert.True(cache.TryGet(user, out var stamp));
        Assert.Equal("new", stamp);
        Assert.Equal(TimeSpan.FromSeconds(10), cache.Lifetime);
    }

    [Fact]
    public void Default_lifetime_is_30_seconds() =>
        Assert.Equal(TimeSpan.FromSeconds(30), new SecurityStampCacheOptions().Seconds * TimeSpan.FromSeconds(1));
}
