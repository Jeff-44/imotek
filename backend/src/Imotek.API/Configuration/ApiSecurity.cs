using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;

namespace Imotek.API.Configuration;

/// <summary>Authentication, CORS, rate limiting and HTTP hardening, driven by configuration.</summary>
public static class ApiSecurity
{
    public const string AuthRateLimit = "auth";
    public const string UploadRateLimit = "uploads";

    /// <summary>Agent phone numbers and new requests: per user, against scraping and spam.</summary>
    public const string ContactRateLimit = "contact";

    /// <summary>New requests, open to visitors without an account: per user, or per address, against spam.</summary>
    public const string RequestRateLimit = "requests";
    public const string CorsPolicy = "frontend";

    /// <summary>
    /// Proxies trusted for X-Forwarded-For/Proto by default: loopback and private networks, which is
    /// where the nginx front end reaches the API from (Docker network, Railway private network).
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultTrustedNetworks =
        ["127.0.0.0/8", "::1/128", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "fc00::/7"];

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
        var contactPermitLimit = configuration.GetValue("RateLimiting:ContactPermitLimit", 30);
        var requestPermitLimit = configuration.GetValue("RateLimiting:RequestPermitLimit", 5);
        // ASPNETCORE_FORWARDEDHEADERS_ENABLED (compose.prod, Railway) turns on X-Forwarded-For/Proto, but
        // on its own it accepts them from any sender, so a client could pick its own IP and escape the
        // rate limits. Only the trusted networks may forward, and only one hop: nginx sets the header itself.
        services.PostConfigure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            var networks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() is { Length: > 0 } configured
                ? configured
                : DefaultTrustedNetworks;
            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }

            foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Tells the client how long to wait, so it can retry instead of treating the refusal as final.
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                return ValueTask.CompletedTask;
            };

            // Login, registration and refresh: per client IP (behind a proxy, enable forwarded headers).
            options.AddPolicy(AuthRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1) }));

            options.AddPolicy(UploadRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = uploadPermitLimit, Window = TimeSpan.FromMinutes(1) }));

            options.AddPolicy(ContactRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = contactPermitLimit, Window = TimeSpan.FromMinutes(1) }));

            options.AddPolicy(RequestRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = requestPermitLimit, Window = TimeSpan.FromMinutes(1) }));
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
