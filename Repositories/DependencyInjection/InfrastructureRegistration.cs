using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Interfaces;
using Repositories.Implements;

namespace Repositories.DependencyInjection
{
    public static class InfrastructureRegistration
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // 1) DbContext (PostgreSQL)
            var connStr = configuration.GetConnectionString("Default")
                          ?? configuration.GetConnectionString("AppDb")
                          ?? configuration.GetConnectionString("StudentGamerHub")
                          ?? throw new InvalidOperationException("Missing ConnectionStrings: Default/AppDb/SchoolHealthManager");

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connStr, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
                })
            );

            // 2) Identity
            services
                .AddIdentityCore<User>(opt =>
                {
                    opt.Password.RequireDigit = true;
                    opt.Password.RequiredLength = 8;
                    opt.Password.RequireNonAlphanumeric = false;
                    opt.Password.RequireUppercase = true;
                    opt.Password.RequireLowercase = true;
                    opt.User.RequireUniqueEmail = true;
                    opt.SignIn.RequireConfirmedEmail = true;
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

            // 6) Hosted seeding
            services.AddHostedService<DbInitializerHostedService>();

            return services;
        }
    }
}
