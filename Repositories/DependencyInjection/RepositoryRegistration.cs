using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Repositories.DependencyInjection
{
    public static class RepositoryRegistration
    {
        public static IServiceCollection AddRepositories(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            if (assemblies is null || assemblies.Length == 0)
                assemblies = new[] { typeof(RepositoryRegistration).Assembly };

            foreach (var asm in assemblies)
            {
                var impls = asm.GetTypes()
                    .Where(t => t.IsClass
                             && !t.IsAbstract
                             && !t.IsGenericTypeDefinition
                             && t.Name.EndsWith("Repository", StringComparison.Ordinal));

                foreach (var impl in impls)
                {
                    var interfaces = impl.GetInterfaces()
                        .Where(i => i.IsInterface
                                 && i.Name.EndsWith("Repository", StringComparison.Ordinal));

                    foreach (var itf in interfaces)
                    {
                        if (!services.Any(d => d.ServiceType == itf))
                            services.AddScoped(itf, impl);
                    }
                }
            }

            return services;
        }
    }
}
