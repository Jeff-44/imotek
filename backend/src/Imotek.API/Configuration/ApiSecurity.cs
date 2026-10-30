using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace Imotek.API.Configuration;

/// <summary>Authentication, CORS, rate limiting and HTTP hardening, driven by configuration.</summary>
public static class ApiSecurity
{
    public const string AuthRateLimit = "auth";
    public const string UploadRateLimit = "uploads";
    public const string CorsPolicy = "frontend";

    public static WebApplicationBuilder AddApiSecurity(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var services = builder.Services;

        // Short-lived access tokens; the client renews them with the refresh token.
        services
            .AddAuthentication(IdentityConstants.BearerScheme)
            .AddBearerToken(IdentityConstants.BearerScheme, options =>
            {
                options.BearerTokenExpiration = TimeSpan.FromMinutes(configuration.GetValue("Auth:AccessTokenMinutes", 30));
                options.RefreshTokenExpiration = TimeSpan.FromDays(configuration.GetValue("Auth:RefreshTokenDays", 7));
            });
        services.AddAuthorization();

        // Tokens are encrypted with Data Protection keys. Without a persistent key store,
        // every container restart signs everybody out and several instances reject each other's tokens.
        var dataProtection = services.AddDataProtection().SetApplicationName("Imotek");
        if (configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Any(o => o.Contains('*')))
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must list explicit origins; wildcards are not allowed.");
        }

        if (origins.Length > 0)
        {
            services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
                .WithOrigins(origins)
                .WithMethods("GET", "POST", "PUT", "DELETE")
                .WithHeaders("Authorization", "Content-Type")));
        }

        var authPermitLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
        var uploadPermitLimit = configuration.GetValue("RateLimiting:UploadPermitLimit", 30);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Login, registration and refresh: per client IP (behind a proxy, enable forwarded headers).
            options.AddPolicy(AuthRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1) }));

            options.AddPolicy(UploadRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = uploadPermitLimit, Window = TimeSpan.FromMinutes(1) }));
        });

        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
        });

        return builder;
    }

    public static WebApplication UseApiSecurity(this WebApplication app)
    {
        app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            // The API never serves HTML: nothing may run or be framed from its responses.
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            return next(context);
        });

        if (!app.Environment.IsDevelopment())
        {
            // HSTS is only sent on HTTPS requests (as seen through X-Forwarded-Proto behind a proxy).
            app.UseHsts();

            // Off when a reverse proxy terminates TLS and already redirects HTTP to HTTPS.
            if (app.Configuration.GetValue("Security:HttpsRedirection", true))
            {
                app.UseHttpsRedirection();
            }
        }

        if ((app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []).Length > 0)
        {
            app.UseCors(CorsPolicy);
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        return app;
    }
}
