using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Implements;
using Repositories.Persistence.Seeding;

namespace Repositories.DependencyInjection
{
    public static class InfrastructureRegistration
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // 1) DbContext (PostgreSQL or InMemory fallback in Development)
            var connStr = configuration.GetConnectionString("Default")
                          ?? configuration.GetConnectionString("AppDb")
                          ?? configuration.GetConnectionString("StudentGamerHub");

            var envName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                           ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            var isDevelopment = string.Equals(envName, "Development", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(connStr))
            {
                if (isDevelopment)
                {
                    // Local dev without PostgreSQL: use in-memory DB so the API can run
                    services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("AppDb"));
                    Console.WriteLine("[WARN] No connection string configured. Using InMemory database in Development.");
                }
                else
                {
                    throw new InvalidOperationException("Missing connection string. Configure ConnectionStrings:Default/AppDb/StudentGamerHub.");
                }
            }
            else
            {
                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(connStr, npgsql =>
                    {
                        npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                        npgsql.MaxBatchSize(1);  // Supavisor proxy disconnects on multi-statement batches
                    })
                );
            }

            // 2) Identity
            services
                .AddIdentityCore<User>(opt =>
                {
                    // Email
                    opt.User.RequireUniqueEmail = true;

                    // ✅ TẮT YÊU CẦU XÁC THỰC EMAIL
                    opt.SignIn.RequireConfirmedEmail = false;

                    // Password
                    opt.Password.RequireDigit = true;
                    opt.Password.RequiredLength = 8;
                    opt.Password.RequireNonAlphanumeric = false;
                    opt.Password.RequireUppercase = true;
                    opt.Password.RequireLowercase = true;

                    // Lockout
                    opt.Lockout.AllowedForNewUsers = true;
                    opt.Lockout.MaxFailedAccessAttempts = 5;
                    opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                })
                .AddRoles<Role>()
                .AddEntityFrameworkStores<AppDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();


            services.Configure<DataProtectionTokenProviderOptions>(o =>
            {
                o.TokenLifespan = TimeSpan.FromHours(2);
            });

            // 3) UoW + open-generic repo (đăng ký 1 nơi)
            services.AddScoped(typeof(IGenericRepository<,>), typeof(GenericRepository<,>));
            services.AddScoped<IRepositoryFactory, RepositoryFactory>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IGenericUnitOfWork, UnitOfWork>();

            // 4) Password hasher for Room (for room password hashing)
            services.AddScoped<IPasswordHasher<Room>, PasswordHasher<Room>>();

            // 5) Specific Repositories (not auto-registered by convention)
            services.AddScoped<ICommunityQueryRepository, CommunityQueryRepository>();
            services.AddScoped<ICommunityCommandRepository, CommunityCommandRepository>();
            services.AddScoped<IRoomQueryRepository, RoomQueryRepository>();
            services.AddScoped<IRoomCommandRepository, RoomCommandRepository>();
            services.AddScoped<IClubQueryRepository, ClubQueryRepository>();
            services.AddScoped<IClubCommandRepository, ClubCommandRepository>();
            services.AddScoped<IEventQueryRepository, EventQueryRepository>();
            services.AddScoped<IEventCommandRepository, EventCommandRepository>();
            services.AddScoped<IRegistrationQueryRepository, RegistrationQueryRepository>();
            services.AddScoped<IRegistrationCommandRepository, RegistrationCommandRepository>();
            services.AddScoped<IEscrowRepository, EscrowRepository>();
            services.AddScoped<IWalletRepository, WalletRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<IPaymentIntentRepository, PaymentIntentRepository>();
            services.AddScoped<IGameRepository, GameRepository>();
            services.AddScoped<IUserGameRepository, UserGameRepository>();
            services.AddScoped<IAppSeeder, AppSeeder>();

            // 6) Seed options binding
            services.Configure<SeedOptions>(configuration.GetSection("Seed"));

            // 7) Hosted seeding
            services.AddHostedService<DbInitializerHostedService>();

            return services;
        }
    }
}
