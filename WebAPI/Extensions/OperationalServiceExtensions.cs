using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.ResponseCompression;
using System.Threading.RateLimiting;

namespace WebApi.Extensions;

public static class OperationalServiceExtensions
{
    public static IServiceCollection AddOperationalServices(
        this IServiceCollection services, IConfiguration _)
    {
        services.AddHealthChecks();                 // /health
        services.AddResponseCompression(o =>        // gzip/br
        {
            o.Providers.Add<GzipCompressionProvider>();
            o.EnableForHttps = true;
        });

        services.AddRateLimiter(o =>               // basic rate limiting
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ctx.User?.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    })));

        services.AddRequestTimeouts(o =>           // timeouts cho endpoint
        {
            o.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
        });

        services.AddResponseCaching();             // cache header-driven
        services.AddHttpContextAccessor();         // hay dùng trong Application/Infra

        return services;
    }
}
