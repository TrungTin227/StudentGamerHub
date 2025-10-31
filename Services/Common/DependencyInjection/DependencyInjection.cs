using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Configuration;
using Services.Presence;
using System.Reflection;

namespace Services.Common.DependencyInjection
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services,
            IConfiguration? configuration = null)
        {
            var asm = typeof(DependencyInjection).Assembly;

            // 1) Core
            services.AddMemoryCache();
            services.AddHttpClient();

            // 2) Options
            if (configuration is not null)
            {
                services.AddOptions<JwtSettings>()
                        .Bind(configuration.GetSection(JwtSettings.SectionName))
                        .ValidateDataAnnotations()
                        .ValidateOnStart();

                services.AddOptions<AuthLinkOptions>()
                        .Bind(configuration.GetSection("AuthLinks"))
                        .Validate(o => !string.IsNullOrWhiteSpace(o.PublicBaseUrl), "AuthLinks:PublicBaseUrl is required")
                        .ValidateOnStart();
                services.AddOptions<GoogleAuthOptions>()
                        .Bind(configuration.GetSection(GoogleAuthOptions.SectionName))
                        .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId), "GoogleAuth:ClientId is required")
                        .ValidateOnStart();
                services.AddOptions<PresenceOptions>()
                        .Bind(configuration.GetSection(PresenceOptions.SectionName))
                        .Validate(o => o.TtlSeconds > 0, "Presence:TtlSeconds must be positive")
                        .Validate(o => o.HeartbeatSeconds > 0, "Presence:HeartbeatSeconds must be positive")
                        .Validate(o => o.GraceSeconds >= 0, "Presence:GraceSeconds cannot be negative")
                        .Validate(o => o.MaxBatchSize > 0, "Presence:MaxBatchSize must be positive")
                        .Validate(o => o.DefaultPageSize > 0, "Presence:DefaultPageSize must be positive")
                        .Validate(o => o.MaxPageSize >= o.DefaultPageSize, "Presence:MaxPageSize must be >= DefaultPageSize")
                        .ValidateOnStart();
                services.AddOptions<BillingOptions>()
                        .Bind(configuration.GetSection(BillingOptions.SectionName))
                        .Validate(o => o.EventCreationFeeCents >= 0, "Billing:EventCreationFeeCents must be >= 0")
                        .Validate(o => o.MaxEventEscrowTopUpAmountCents > 0, "Billing:MaxEventEscrowTopUpAmountCents must be positive")
                        .Validate(o => o.MaxWalletTopUpAmountCents > 0, "Billing:MaxWalletTopUpAmountCents must be positive")
                        .ValidateOnStart();
                // KHÔNG đăng ký singleton .Value để giữ hot-reload
            }

            // 3) Utils
            services.AddSingleton<ITimeZoneService, TimeZoneService>();
            services.AddScoped<IAuthEmailFactory, AuthEmailFactory>();

            // 4) Validators / AutoMapper
            services.AddValidatorsFromAssemblies(
                new[]
                {
                        asm,                                     // Services assembly
                        typeof(LoginRequestValidator).Assembly   // DTOs.Auth.Validation assembly
                },
                includeInternalTypes: true
            );
            // services.AddAutoMapper(asm);

            // 5) Convention registrations
            RegisterCrudServices(services, asm);
            RegisterServiceInterfacesByConvention(services, asm);

            services.AddScoped<IPresenceReader, RedisPresenceReader>();

            return services;
        }

        private static void RegisterCrudServices(IServiceCollection services, Assembly asm)
        {
            var crudOpenGeneric = typeof(ICrudService<,,,>);
            var impls = asm.GetTypes().Where(t => t.IsClass && !t.IsAbstract);

            foreach (var impl in impls)
            {
                var crudIfaces = impl.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == crudOpenGeneric);

                foreach (var i in crudIfaces)
                    services.AddScoped(i, impl);
            }
        }

        private static void RegisterServiceInterfacesByConvention(IServiceCollection services, Assembly asm)
        {
            var impls = asm.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Service", StringComparison.Ordinal));

            foreach (var impl in impls)
            {
                var interfaces = impl.GetInterfaces()
                    .Where(i => i.Name.EndsWith("Service", StringComparison.Ordinal));

                var lifetime =
                    typeof(ISingletonService).IsAssignableFrom(impl) ? ServiceLifetime.Singleton :
                    typeof(ITransientService).IsAssignableFrom(impl) ? ServiceLifetime.Transient :
                    ServiceLifetime.Scoped;

                foreach (var itf in interfaces)
                {
                    if (!services.Any(d => d.ServiceType == itf))
                        services.Add(new ServiceDescriptor(itf, impl, lifetime));
                }
            }
        }
    }
}
