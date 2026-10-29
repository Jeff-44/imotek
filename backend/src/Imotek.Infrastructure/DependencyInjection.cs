using Imotek.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Imotek.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "ImotekDb";
    public const string DatabaseHealthCheckName = "database";
    public const string DatabaseHealthCheckTag = "db";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. " +
                "Set it with user-secrets or the ConnectionStrings__ImotekDb environment variable.");
        }

        services.AddDbContext<ImotekDbContext>(options => options.UseNpgsql(connectionString));

        services.AddHealthChecks()
            .AddDbContextCheck<ImotekDbContext>(DatabaseHealthCheckName, tags: [DatabaseHealthCheckTag]);

        return services;
    }
}
