using Imotek.Application.Auth;
using Imotek.Application.Images;
using Imotek.Application.Properties;
using Imotek.Infrastructure.Identity;
using Imotek.Infrastructure.Images;
using Imotek.Infrastructure.Persistence;
using Imotek.Infrastructure.Persistence.Repositories;
using Imotek.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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

        // The authentication scheme itself is registered by the API (host concern).
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ImotekDbContext>()
            .AddSignInManager();

        services.AddScoped<IAuthService, IdentityAuthService>();
        services.AddScoped<IPropertyRepository, PropertyRepository>();

        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddSingleton<IFileStorage>(sp => new LocalFileStorage(
            sp.GetRequiredService<IOptions<FileStorageOptions>>(),
            sp.GetRequiredService<IHostEnvironment>().ContentRootPath));
        services.AddSingleton<IImageProcessor, ImageProcessor>();

        return services;
    }
}
