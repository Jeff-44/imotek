using Imotek.Infrastructure;
using Imotek.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Imotek.Tests.Infrastructure;

public class DatabaseConnectionStringTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
            .Build();

    [Fact]
    public void Railway_style_url_becomes_an_npgsql_connection_string()
    {
        var connectionString = DatabaseConnectionString.FromUrl(
            "postgresql://postgres:s3cr%40t%3Apass@imotek-db.railway.internal:5432/railway");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Equal("imotek-db.railway.internal", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("railway", builder.Database);
        Assert.Equal("postgres", builder.Username);
        Assert.Equal("s3cr@t:pass", builder.Password); // percent-encoded characters are decoded
        Assert.Equal(GssEncryptionMode.Disable, builder.GssEncryptionMode);
    }

    [Theory]
    [InlineData("postgres://u:p@db.example:6543/app", 6543)]
    [InlineData("postgresql://u:p@db.example/app", 5432)]
    public void Accepts_both_schemes_and_defaults_the_port(string url, int expectedPort)
    {
        var builder = new NpgsqlConnectionStringBuilder(DatabaseConnectionString.FromUrl(url));

        Assert.Equal(expectedPort, builder.Port);
        Assert.Equal("app", builder.Database);
    }

    [Theory]
    [InlineData("require", SslMode.Require)]
    [InlineData("verify-full", SslMode.VerifyFull)]
    [InlineData("disable", SslMode.Disable)]
    public void Honors_sslmode(string sslmode, SslMode expected)
    {
        var builder = new NpgsqlConnectionStringBuilder(
            DatabaseConnectionString.FromUrl($"postgresql://u:p@db.example:5432/app?sslmode={sslmode}"));

        Assert.Equal(expected, builder.SslMode);
    }

    [Fact]
    public void Ipv6_host_literal_is_unbracketed()
    {
        var builder = new NpgsqlConnectionStringBuilder(DatabaseConnectionString.FromUrl("postgresql://u:p@[fd12::1]:5432/app"));

        Assert.Equal("fd12::1", builder.Host);
    }

    [Theory]
    [InlineData("mysql://u:p@db.example/app")]
    [InlineData("not a url")]
    [InlineData("postgresql://u:p@db.example:5432/")]
    [InlineData("postgresql://u:p@db.example/app?sslmode=bogus")]
    public void Invalid_urls_fail_without_leaking_the_value(string url)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DatabaseConnectionString.FromUrl(url));

        Assert.Contains(DatabaseConnectionString.DatabaseUrlKey, ex.Message);
        Assert.DoesNotContain("u:p", ex.Message);
        Assert.DoesNotContain("db.example", ex.Message);
    }

    [Fact]
    public void Explicit_connection_string_wins_over_DATABASE_URL()
    {
        var configuration = Config(
            ($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", "Host=explicit;Database=a;Username=b"),
            (DatabaseConnectionString.DatabaseUrlKey, "postgresql://u:p@from-url/app"));

        Assert.Contains("explicit", DatabaseConnectionString.Resolve(configuration));
    }

    [Fact]
    public void Falls_back_to_DATABASE_URL_and_null_when_nothing_is_set()
    {
        var fromUrl = DatabaseConnectionString.Resolve(Config((DatabaseConnectionString.DatabaseUrlKey, "postgresql://u:p@from-url/app")));

        Assert.Equal("from-url", new NpgsqlConnectionStringBuilder(fromUrl).Host);
        Assert.Null(DatabaseConnectionString.Resolve(Config()));
    }
}
