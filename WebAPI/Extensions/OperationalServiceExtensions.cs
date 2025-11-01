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

        // GlobalLimiter removed - using endpoint-specific rate limiting policies instead
        // to avoid conflicts and allow proper webhook handling

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
