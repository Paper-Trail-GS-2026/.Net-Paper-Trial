using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PaperTrail.Infrastructure.Persistence;

namespace PaperTrail.Infrastructure.Health;

// The database is required by every private endpoint, so a failure is Unhealthy.
public sealed class DatabaseHealthCheck(PaperTrailContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            return await context.Database.CanConnectAsync(timeout.Token)
                ? HealthCheckResult.Healthy("Database is reachable.")
                : HealthCheckResult.Unhealthy("Database is not reachable.");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // No exception details: the result is public.
            return HealthCheckResult.Unhealthy("Database is not reachable.");
        }
    }
}
