using System.Net;
using System.Net.Http.Json;
using Imotek.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Imotek.Tests.Api;

/// <summary>
/// Behind nginx, ASPNETCORE_FORWARDEDHEADERS_ENABLED is on (compose.prod, Railway). X-Forwarded-For must
/// then be taken only from the trusted proxy networks, one hop deep, and rate limits must use that address.
/// </summary>
public class ForwardedHeadersTests(ForwardedHeadersTests.Factory factory) : IClassFixture<ForwardedHeadersTests.Factory>
{
    public const int AuthLimit = 2;

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}",
                "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1");
            builder.UseSetting("FORWARDEDHEADERS_ENABLED", "true");
            builder.UseSetting("RateLimiting:AuthPermitLimit", AuthLimit.ToString());
        }
    }

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private ForwardedHeadersOptions Options => factory.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

    [Fact]
    public void Only_private_networks_may_forward_and_only_one_hop()
    {
        var options = Options;

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal(1, options.ForwardLimit);
        Assert.Contains(System.Net.IPNetwork.Parse("10.0.0.0/8"), options.KnownIPNetworks);
        Assert.DoesNotContain(options.KnownIPNetworks, n => n.PrefixLength == 0);
    }

    [Theory]
    [InlineData("10.0.0.5", "203.0.113.7", "203.0.113.7")] // private network (Docker, Railway)
    [InlineData("172.18.0.3", "203.0.113.7", "203.0.113.7")]
    [InlineData("fd12:3456::1", "203.0.113.7", "203.0.113.7")] // IPv6 private network
    [InlineData("::ffff:10.0.0.5", "203.0.113.7", "203.0.113.7")] // IPv4 seen through a dual-stack socket
    [InlineData("198.51.100.4", "203.0.113.7", "198.51.100.4")] // a public sender cannot choose its address
    [InlineData("10.0.0.5", "1.2.3.4, 203.0.113.7", "203.0.113.7")] // a spoofed extra entry is ignored
    public async Task Client_address_comes_from_the_trusted_proxy_only(string remote, string forwardedFor, string expected)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Microsoft.Extensions.Options.Options.Create(Options));

        await middleware.Invoke(context);

        var actual = context.Connection.RemoteIpAddress!;
        Assert.Equal(IPAddress.Parse(expected), actual.IsIPv4MappedToIPv6 ? actual.MapToIPv4() : actual);
    }

    [Fact]
    public async Task Configured_networks_replace_the_defaults()
    {
        await using var custom = factory.WithWebHostBuilder(b => b.UseSetting("ForwardedHeaders:KnownNetworks:0", "100.64.0.0/10"));
        var options = custom.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Equal([System.Net.IPNetwork.Parse("100.64.0.0/10")], options.KnownIPNetworks);
    }

    [Fact]
    public async Task Rate_limited_responses_say_when_to_retry()
    {
        var client = factory.CreateClient();

        HttpResponseMessage last = null!;
        for (var i = 0; i <= AuthLimit; i++)
        {
            last = await client.PostAsJsonAsync("/api/auth/register", new { }, _ct);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
        var retryAfter = int.Parse(last.Headers.GetValues("Retry-After").Single());
        Assert.InRange(retryAfter, 1, 60);
    }
}
