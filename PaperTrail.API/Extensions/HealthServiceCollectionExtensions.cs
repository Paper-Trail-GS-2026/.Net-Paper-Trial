using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PaperTrail.Infrastructure.Health;

namespace PaperTrail.API.Extensions;

public static class HealthServiceCollectionExtensions
{
    public const string Route = "/health";

    public static IServiceCollection AddPaperTrailHealth(this IServiceCollection services)
    {
        services.AddHttpClient<CatalogHealthCheck>((provider, client) =>
        {
            var url = provider.GetRequiredService<IConfiguration>()["JavaCatalog:BaseUrl"];
            if (!Uri.TryCreate(url, UriKind.Absolute, out var address))
                throw new InvalidOperationException("Configure JavaCatalog:BaseUrl.");
            client.BaseAddress = address;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy)
            .AddCheck<CatalogHealthCheck>("catalog", HealthStatus.Degraded);
        return services;
    }

    // Public endpoint with status names only: no messages, exceptions or connection data.
    public static IEndpointRouteBuilder MapPaperTrailHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(Route, new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
                }, JsonSerializerOptions.Web);
            }
        }).DisableRateLimiting();
        return endpoints;
    }
}
