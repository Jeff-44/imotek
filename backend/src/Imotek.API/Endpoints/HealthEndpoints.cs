using Imotek.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Imotek.API.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Liveness: the API process is up. Runs no dependency checks.
        endpoints.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponse,
        });

        // Readiness of PostgreSQL. Returns 503 when the database is unreachable.
        endpoints.MapHealthChecks("/health/db", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(DependencyInjection.DatabaseHealthCheckTag),
            ResponseWriter = WriteResponse,
        });

        return endpoints;
    }

    // Only names and statuses are returned; exception messages and descriptions
    // may contain connection details and are never written to the response.
    private static Task WriteResponse(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new HealthResponse(
            report.Status.ToString(),
            report.Entries.Select(e => new HealthCheckEntry(e.Key, e.Value.Status.ToString())).ToArray()));

    private sealed record HealthResponse(string Status, HealthCheckEntry[] Checks);

    private sealed record HealthCheckEntry(string Name, string Status);
}
