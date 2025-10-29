using BusinessObjects;
using BusinessObjects.Common.Results;
using DTOs.Quests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repositories.Interfaces;
using Repositories.Persistence;
using Repositories.Persistence.Seeding;
using Repositories.WorkSeeds.Extensions;
using Repositories.WorkSeeds.Interfaces;
using Services.Application.Quests;
using Services.Configuration;
using Services.Implementations;
using Services.Interfaces;
using StackExchange.Redis;
using System.IO;
using System.Net.Http;
using System.Text.Json.Serialization;

namespace WebApi.Payments.Tests.Infrastructure;

public sealed class PaymentsApiFactory : WebApplicationFactory<global::Program>
{
    public const string SecretKey = "integration-secret";
    public static readonly Guid DefaultUserId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    private readonly string _databaseName = $"AppDb-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["Seed:Run"] = "false",
                ["Seed:ApplyMigrations"] = "false",
                ["PayOS:SecretKey"] = SecretKey,
                ["PayOS:ChecksumKey"] = SecretKey,
                ["PayOS:WebhookSecret"] = SecretKey,
                ["PayOS:FrontendBaseUrl"] = "https://frontend.test.local",
                ["PayOS:ReturnUrl"] = "https://api.test.local/api/payments/payos/return",
                ["PayOS:WebhookUrl"] = "https://api.test.local/api/payments/payos/webhook",
                ["PayOS:WebhookTolerance"] = "00:10:00",
                ["PayOS:WebhookToleranceSeconds"] = "600",
                ["JwtSettings:ValidIssuer"] = "https://localhost:7227",
                ["JwtSettings:ValidAudience"] = "UserManager",
                ["JwtSettings:Key"] = "test-signing-key-value-should-be-long",
                ["ConnectionStrings:Default"] = string.Empty,
                ["ConnectionStrings:AppDb"] = string.Empty,
                ["ConnectionStrings:StudentGamerHub"] = string.Empty
            };

            configBuilder.AddInMemoryCollection(overrides!);
        });

        builder.ConfigureServices(services =>
        {
            // Replace AppDbContext registration with isolated in-memory database per factory
            var contextDescriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(AppDbContext))
                .ToList();
            foreach (var descriptor in contextDescriptors)
            {
                services.Remove(descriptor);
            }
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<IConfigureOptions<DbContextOptions<AppDbContext>>>();
            services.RemoveAll<IPostConfigureOptions<DbContextOptions<AppDbContext>>>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });

            services.Configure<SeedOptions>(opt =>
            {
                opt.Run = false;
                opt.ApplyMigrations = false;
            });

            // Remove database initializer hosted service
            var hostedDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService) &&
                            d.ImplementationType == typeof(DbInitializerHostedService))
                .ToList();
            foreach (var descriptor in hostedDescriptors)
            {
                services.Remove(descriptor);
            }

            // Remove Redis-specific services registered in production
            services.RemoveAll<IConnectionMultiplexer>();

            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var options = new ConfigurationOptions
                {
                    AbortOnConnectFail = false,
                    ConnectRetry = 0,
                    ConnectTimeout = 200,
                    SyncTimeout = 200
                };
                options.EndPoints.Add("localhost", 6379);

                try
                {
                    return ConnectionMultiplexer.Connect(options);
                }
                catch
                {
                    return ConnectionMultiplexer.Connect(options, TextWriter.Null);
                }
            });

            var hubLifetimeDescriptors = services
                .Where(d => d.ServiceType.IsGenericType &&
                            d.ServiceType.GetGenericTypeDefinition() == typeof(HubLifetimeManager<>))
                .ToList();
            foreach (var descriptor in hubLifetimeDescriptors)
            {
                services.Remove(descriptor);
            }
            services.AddSingleton(typeof(HubLifetimeManager<>), typeof(DefaultHubLifetimeManager<>));

            // Override quest service with lightweight stub
            services.RemoveAll<IQuestService>();
            services.AddSingleton<IQuestService, StubQuestService>();

            // Override wallet repository to avoid ExecuteUpdate reliance in tests
            services.RemoveAll<IWalletRepository>();
            services.AddScoped<IWalletRepository, TestWalletRepository>();

            // Override IPayOsService to disable Redis dependency
            services.RemoveAll<IPayOsService>();
            services.AddHttpClient("PayOsTestClient");
            services.AddTransient<IPayOsService>(sp =>
            {
                var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("PayOsTestClient");
                return new PayOsService(
                    client,
                    sp.GetRequiredService<IOptionsSnapshot<PayOsOptions>>(),
                    sp.GetRequiredService<IMemoryCache>(),
                    redis: null,
                    sp.GetRequiredService<ILogger<PayOsService>>(),
                    sp.GetRequiredService<IGenericUnitOfWork>(),
                    sp.GetRequiredService<IPaymentIntentRepository>(),
                    sp.GetRequiredService<IRegistrationQueryRepository>(),
                    sp.GetRequiredService<IRegistrationCommandRepository>(),
                    sp.GetRequiredService<IEventQueryRepository>(),
                    sp.GetRequiredService<ITransactionRepository>(),
                    sp.GetRequiredService<IWalletRepository>(),
                    sp.GetRequiredService<IEscrowRepository>(),
                    sp.GetRequiredService<IQuestService>());
            });

            // Ensure database schema is created
            var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });

        builder.ConfigureTestServices(services =>
        {
            services.Configure<JsonOptions>(opt =>
            {
                opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthenticationScheme, _ => { });

            services.AddAuthorization();
        });
    }

    public new HttpClient CreateClient()
        => base.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    private sealed class StubQuestService : IQuestService
    {
        public Task<Result> CompleteCheckInAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(Result.Success());

        public Task<Result<QuestTodayDto>> GetTodayAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(Result<QuestTodayDto>.Success(new QuestTodayDto(0, Array.Empty<QuestItemDto>())));

        public Task<Result> MarkAttendEventAsync(Guid userId, Guid eventId, CancellationToken ct = default)
            => Task.FromResult(Result.Success());

        public Task<Result> MarkInviteAcceptedAsync(Guid inviterId, Guid recipientId, CancellationToken ct = default)
            => Task.FromResult(Result.Success());

        public Task<Result> MarkJoinRoomAsync(Guid userId, Guid roomId, CancellationToken ct = default)
            => Task.FromResult(Result.Success());
    }

    private sealed class TestWalletRepository : IWalletRepository
    {
        private readonly AppDbContext _context;

        public TestWalletRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateIfMissingAsync(Guid userId, CancellationToken ct = default)
        {
            var exists = await _context.Wallets
                .AsNoTracking()
                .AnyAsync(w => w.UserId == userId, ct)
                .ConfigureAwait(false);

            if (exists)
            {
                return;
            }

            var wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = userId
            };

            await _context.Wallets.AddAsync(wallet, ct).ConfigureAwait(false);
        }

        public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
            => _context.Wallets
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.UserId == userId, ct);

        public async Task<bool> AdjustBalanceAsync(Guid userId, long deltaCents, CancellationToken ct = default)
        {
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId, ct).ConfigureAwait(false);
            if (wallet is null)
            {
                return false;
            }

            if (deltaCents < 0 && wallet.BalanceCents + deltaCents < 0)
            {
                return false;
            }

            wallet.BalanceCents += deltaCents;
            wallet.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }
    }
}
