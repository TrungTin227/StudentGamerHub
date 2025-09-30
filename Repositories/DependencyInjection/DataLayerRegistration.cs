using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Repositories.DependencyInjection
{
    public static class DataLayerRegistration
    {
        // Overload mặc định: scan assembly nơi đặt các Repository (dùng marker)
        public static IServiceCollection AddDataLayer(
            this IServiceCollection services,
            IConfiguration configuration)
            => services.AddDataLayer(configuration, typeof(AssemblyMarker).Assembly);

        // Overload linh hoạt: nhận nhiều assembly chứa Repo
        public static IServiceCollection AddDataLayer(
            this IServiceCollection services,
            IConfiguration configuration,
            params Assembly[] repoAssemblies)
        {
            services.AddInfrastructureServices(configuration);
            services.AddRepositories(repoAssemblies);
            return services;
        }
    }
}
