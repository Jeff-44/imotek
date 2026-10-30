using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Imotek.Infrastructure.Persistence;

/// <summary>
/// Finds the PostgreSQL connection string: <c>ConnectionStrings:ImotekDb</c> first, then a
/// <c>DATABASE_URL</c> in URL form (postgresql://user:password@host:port/database), as provided
/// by Railway and most PaaS. Error messages never include the value, which holds the password.
/// </summary>
public static class DatabaseConnectionString
{
    public const string DatabaseUrlKey = "DATABASE_URL";

    public static string? Resolve(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DependencyInjection.ConnectionStringName);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var url = configuration[DatabaseUrlKey];
        return string.IsNullOrWhiteSpace(url) ? null : FromUrl(url);
    }

    public static string FromUrl(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("postgres" or "postgresql"))
        {
            throw new InvalidOperationException($"{DatabaseUrlKey} must be a postgres:// or postgresql:// URL.");
        }

        if (string.IsNullOrEmpty(uri.Host))
        {
            throw new InvalidOperationException($"{DatabaseUrlKey} has no host.");
        }

        var credentials = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host.Trim('[', ']'), // IPv6 literals come bracketed
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null,
            // No Kerberos on PaaS databases; avoids a failed GSS negotiation on every connection.
            GssEncryptionMode = GssEncryptionMode.Disable,
        };

        if (string.IsNullOrEmpty(builder.Database))
        {
            throw new InvalidOperationException($"{DatabaseUrlKey} has no database name.");
        }

        foreach (var (key, value) in ParseQuery(uri.Query))
        {
            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            {
                builder.SslMode = value.ToLowerInvariant() switch
                {
                    "disable" => SslMode.Disable,
                    "allow" => SslMode.Allow,
                    "prefer" => SslMode.Prefer,
                    "require" => SslMode.Require,
                    "verify-ca" => SslMode.VerifyCA,
                    "verify-full" => SslMode.VerifyFull,
                    _ => throw new InvalidOperationException($"{DatabaseUrlKey} has an unsupported sslmode."),
                };
            }
        }

        return builder.ConnectionString;
    }

    private static IEnumerable<(string Key, string Value)> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Select(parts => (Uri.UnescapeDataString(parts[0]), parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty));
}
