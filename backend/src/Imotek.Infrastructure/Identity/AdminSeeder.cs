using Imotek.Application.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Imotek.Infrastructure.Identity;

/// <summary>
/// Creates (or promotes) the administrator account from configuration.
/// Admins cannot register through the API, so this is the only way to get one.
/// </summary>
public static class AdminSeeder
{
    public const string EmailKey = "Seed:AdminEmail";
    public const string PasswordKey = "Seed:AdminPassword";

    public static async Task SeedAdminAsync(this IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var email = configuration[EmailKey];
        var password = configuration[PasswordKey];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);
        if (user is not null && !scope.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            // The password is only used to create the account; it is never reset from configuration.
            logger.LogWarning(
                "{PasswordKey} is still configured although the admin account exists. Remove it from the environment.",
                PasswordKey);
        }

        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, FullName = "Administrateur" };
            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                logger.LogError("Admin account could not be created: {Errors}", string.Join(" ", created.Errors.Select(e => e.Code)));
                return;
            }
        }

        if (!await userManager.IsInRoleAsync(user, Roles.Admin))
        {
            await userManager.AddToRoleAsync(user, Roles.Admin);
        }

        logger.LogInformation("Admin account is ready.");
    }
}
