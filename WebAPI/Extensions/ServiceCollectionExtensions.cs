using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using System.Threading.RateLimiting;
using Services.Implementations.Memberships;
using Services.Interfaces;

namespace WebApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

        // Ensure API explorer for controllers is available for OpenAPI/Scalar
        services.AddEndpointsApiExplorer();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("GamesWrite", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 60,
                    TokensPerPeriod = 60,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.AddPolicy("GamesRead", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 120,
                    TokensPerPeriod = 120,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.AddPolicy("EventsWrite", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 60,
                    TokensPerPeriod = 60,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.AddPolicy("RegistrationsWrite", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 60,
                    TokensPerPeriod = 60,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.AddPolicy("PaymentsWrite", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 120,
                    TokensPerPeriod = 120,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.AddPolicy("PaymentsWebhook", httpContext =>
            {
                var ipKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetTokenBucketLimiter(ipKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 300,
                    TokensPerPeriod = 300,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.AddPolicy("ReadsHeavy", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 300,
                    TokensPerPeriod = 300,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.AddPolicy("ReadsLight", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 600,
                    TokensPerPeriod = 600,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.AddPolicy("PresenceAdminReads", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 60,
                    TokensPerPeriod = 60,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.AddPolicy("PresenceBatchReads", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 200,
                    TokensPerPeriod = 200,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.AddPolicy("FriendInvite", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 20,
                    TokensPerPeriod = 20,
                    ReplenishmentPeriod = TimeSpan.FromDays(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.AddPolicy("FriendAction", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 60,
                    TokensPerPeriod = 60,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            options.AddPolicy("DashboardRead", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 120,
                    TokensPerPeriod = 120,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            // Teammates search rate limiting: 120 requests per minute per user
            options.AddPolicy("TeammatesRead", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 120,
                    TokensPerPeriod = 120,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            // Room creation rate limiting: 10 requests per day per user
            options.AddPolicy("RoomsCreate", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 10,
                    TokensPerPeriod = 10,
                    ReplenishmentPeriod = TimeSpan.FromDays(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            // Room read rate limiting: 120 requests per minute per user
            options.AddPolicy("RoomsRead", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 120,
                    TokensPerPeriod = 120,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            // Room write rate limiting: 30 requests per minute per user
            options.AddPolicy("RoomsWrite", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 30,
                    TokensPerPeriod = 30,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            // Room archive rate limiting: 10 requests per day per user
            options.AddPolicy("RoomsArchive", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 10,
                    TokensPerPeriod = 10,
                    ReplenishmentPeriod = TimeSpan.FromDays(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            // Communities read rate limiting: 120 requests per minute per user
            options.AddPolicy("CommunitiesRead", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 120,
                    TokensPerPeriod = 120,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            // Communities write rate limiting: 10 requests per day per user
            options.AddPolicy("CommunitiesWrite", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 10,
                    TokensPerPeriod = 10,
                    ReplenishmentPeriod = TimeSpan.FromDays(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            // Clubs read rate limiting: 120 requests per minute per user
            options.AddPolicy("ClubsRead", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 120,
                    TokensPerPeriod = 120,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            // Clubs write rate limiting (create/update/archive): 10 requests per day per user
            options.AddPolicy("ClubsWrite", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 10,
                    TokensPerPeriod = 10,
                    ReplenishmentPeriod = TimeSpan.FromDays(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

            // Bug reports write rate limiting: 20 requests per day per user
            options.AddPolicy("BugsWrite", httpContext =>
            {
                var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

                return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 20,
                    TokensPerPeriod = 20,
                    ReplenishmentPeriod = TimeSpan.FromDays(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });

        });

        services.TryAddSingleton<IConnectionMultiplexer>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetValue<string>("Redis:ConnectionString")
                                   ?? configuration["Redis__ConnectionString"];

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // Development fallback: return a null implementation
                var env = sp.GetService<IHostEnvironment>();
                if (env?.IsDevelopment() == true)
                {
                    throw new InvalidOperationException(
                        "Redis:ConnectionString is not configured. " +
                        "Please install Redis or configure connection string in appsettings.Development.json");
                }
                throw new InvalidOperationException("Redis:ConnectionString (Redis__ConnectionString) is required");
            }

            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 5000;
            options.SyncTimeout = 5000;

            return ConnectionMultiplexer.Connect(options);
        });

        // Chat configuration and services
        services.Configure<Services.Configuration.ChatOptions>(configuration.GetSection("Chat"));
        services.AddSingleton<Services.Interfaces.IChatHistoryService, Services.Implementations.ChatHistoryService>();

        // SignalR with Redis backplane
        var redisConn = configuration.GetSection("Redis")["ConnectionString"] ?? "localhost:6379";
        services.AddSignalR().AddStackExchangeRedis(redisConn, _ => { });

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer(new BearerSecuritySchemeTransformer());
            options.AddDocumentTransformer(new FriendsExamplesDocumentTransformer());
        });

        services.AddProblemDetails();
        services.AddExceptionHandler<AppExceptionHandler>();

        services.AddScoped<IMembershipReadService, MembershipReadService>();

        // PayOS configuration and service
        services.Configure<Services.Configuration.PayOsConfig>(configuration.GetSection("PayOS"));
        services.AddHttpClient<IPayOsService, Services.Implementations.PayOsService>();

        services.AddCors(opt =>
        {
            opt.AddPolicy("Default", p =>
            {
                // Read allowed origins from configuration: Cors:AllowedOrigins as string[]
                var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                                     ?? Array.Empty<string>();

                if (allowedOrigins.Length == 0 || Array.Exists(allowedOrigins, o => o == "*"))
                {
                    // Fallback: allow any origin (no credentials allowed with wildcard)
                    p.AllowAnyOrigin()
                     .AllowAnyHeader()
                     .AllowAnyMethod();
                }
                else
                {
                    // Explicit origins: allow credentials for SPA auth/cookies if needed
                    p.WithOrigins(allowedOrigins)
                     .AllowAnyHeader()
                     .AllowAnyMethod()
                     .AllowCredentials();
                }
            });
        });

        services.Configure<ApiBehaviorOptions>(opt =>
        {
            opt.SuppressModelStateInvalidFilter = false;
        });

        return services;
    }
}
