using Microsoft.Extensions.DependencyInjection;
using Services.Common.Caching;

namespace Services.Common.DependencyInjection;

public static class CachingServiceCollectionExtensions
{
    /// <summary>
    /// Add caching services (Redis-based distributed cache).
    /// </summary>
    public static IServiceCollection AddCachingServices(this IServiceCollection services)
    {
        // Register Redis-based cache service
        services.AddSingleton<ICacheService, RedisCacheService>();

        return services;
    }
}
