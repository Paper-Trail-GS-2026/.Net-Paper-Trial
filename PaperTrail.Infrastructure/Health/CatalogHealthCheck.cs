using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PaperTrail.Infrastructure.Health;

// The Java catalog is only used to create preparations. When it is down, listing and editing
// still work, so the registration sets FailureStatus to Degraded and this check reports it.
// Any HTTP answer below 500 means the service is reachable (the base URL may need a token).
public sealed class CatalogHealthCheck(HttpClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await client.GetAsync("", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            return (int)response.StatusCode < 500
                ? HealthCheckResult.Healthy("Catalog is reachable.")
                : new HealthCheckResult(context.Registration.FailureStatus, "Catalog returned a server error.");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Catalog is not reachable.");
        }
    }
}
