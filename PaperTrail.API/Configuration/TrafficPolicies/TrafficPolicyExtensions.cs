using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace PaperTrail.API.Configuration.TrafficPolicies;

public static class TrafficPolicyExtensions
{
    private const int DefaultPermitLimit = 100;
    private const int DefaultWindowSeconds = 60;

    // One fixed window per signed-in user (sub claim) or, without a valid token, per remote address.
    // Behind a proxy the address is the proxy's unless forwarded headers are configured by the host.
    public static IServiceCollection AddPaperTrailRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
                var permitLimit = Math.Max(1, configuration.GetValue("RateLimiting:PermitLimit", DefaultPermitLimit));
                var windowSeconds = Math.Max(1, configuration.GetValue("RateLimiting:WindowSeconds", DefaultWindowSeconds));
                var subject = context.User.FindFirst("sub")?.Value;
                var key = subject is not null ? $"user:{subject}"
                    : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0
                });
            });
            options.OnRejected = async (rejected, cancellationToken) =>
            {
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    rejected.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
                    detail: "Too many requests. Try again later.", instance: rejected.HttpContext.Request.Path)
                    .ExecuteAsync(rejected.HttpContext);
            };
        });
        return services;
    }
}
