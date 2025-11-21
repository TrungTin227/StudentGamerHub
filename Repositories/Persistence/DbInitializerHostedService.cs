using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repositories.Interfaces;
using Repositories.Persistence.Seeding;

namespace Repositories.Persistence;

public sealed class DbInitializerHostedService : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<DbInitializerHostedService> _logger;
    private readonly IHostEnvironment _env;
    private readonly SeedOptions _opt;

    public DbInitializerHostedService(
        IServiceProvider serviceProvider,
        ILogger<DbInitializerHostedService> logger,
        IHostEnvironment env,
        IOptions<SeedOptions> options)
    {
        _sp = serviceProvider;
        _logger = logger;
        _env = env;
        _opt = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Database initialization started. (Env: {Env})", _env.EnvironmentName);

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            // Migrations luôn chạy nếu ApplyMigrations = true (độc lập với Seed:Run)
            if (_opt.ApplyMigrations)
            {
                await db.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Migrations applied successfully.");
            }

            // Guard seeding theo môi trường & config
            if (!_opt.Run)
            {
                _logger.LogInformation("Seeding is disabled by configuration (Seed:Run = false). Skipped.");
                return;
            }

            if (_env.IsProduction() && !_opt.AllowInProduction)
            {
                _logger.LogWarning("Seeding is disabled in Production (Seed:AllowInProduction = false). Skipped.");
                return;
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            var walletRepository = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var seeder = scope.ServiceProvider.GetService<IAppSeeder>();

            await EnsureRolesAsync(roleManager, _opt.Roles, cancellationToken);
            await EnsureAdminAsync(userManager, roleManager, _opt.Admin, cancellationToken);

            var platformUserIdValue = configuration.GetValue<string>("Billing:PlatformUserId");
            if (!string.IsNullOrWhiteSpace(platformUserIdValue) &&
                Guid.TryParse(platformUserIdValue, out var platformUserId) &&
                platformUserId != Guid.Empty)
            {
                await EnsurePlatformWalletAsync(userManager, walletRepository, db, platformUserId, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                _logger.LogInformation("Billing:PlatformUserId is not configured. Platform wallet will be created on demand.");
            }

            if (seeder is not null)
            {
                await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Database seeding completed successfully.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Database initialization canceled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during database initialization.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // ---------- helpers ----------

    private async Task EnsurePlatformWalletAsync(
        UserManager<User> userManager,
        IWalletRepository walletRepository,
        AppDbContext db,
        Guid platformUserId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var email = $"platform+{platformUserId:N}@studentgamerhub.local";
        var userName = $"platform-{platformUserId:N}";
        var platformUser = await userManager.FindByIdAsync(platformUserId.ToString());

        if (platformUser is null)
        {
            platformUser = new User
            {
                Id = platformUserId,
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                FullName = "Platform Wallet",
                CreatedAtUtc = DateTime.UtcNow,
                IsDeleted = false
            };

            var createResult = await userManager.CreateAsync(platformUser);
            if (!createResult.Succeeded)
            {
                _logger.LogError(
                    "Failed to create platform user {PlatformUserId}: {Errors}",
                    platformUserId,
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return;
            }

            _logger.LogInformation("Platform user created with id {PlatformUserId}.", platformUserId);
        }
        else
        {
            var needsUpdate = false;

            if (!string.Equals(platformUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                platformUser.Email = email;
                platformUser.UserName = userName;
                needsUpdate = true;
            }

            if (!platformUser.EmailConfirmed)
            {
                platformUser.EmailConfirmed = true;
                needsUpdate = true;
            }

            if (platformUser.IsDeleted)
            {
                platformUser.IsDeleted = false;
                platformUser.DeletedAtUtc = null;
                platformUser.DeletedBy = null;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                var updateResult = await userManager.UpdateAsync(platformUser);
                if (!updateResult.Succeeded)
                {
                    _logger.LogWarning(
                        "Failed to synchronize platform user {PlatformUserId}: {Errors}",
                        platformUserId,
                        string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                }
            }
        }

        await walletRepository.CreateIfMissingAsync(platformUserId, ct).ConfigureAwait(false);
        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("Platform wallet created for user {PlatformUserId}.", platformUserId);
        }
        else
        {
            _logger.LogInformation("Platform wallet already exists for user {PlatformUserId}.", platformUserId);
        }
    }

    private async Task EnsureRolesAsync(RoleManager<Role> roleManager, IEnumerable<string> roles, CancellationToken ct)
    {
        foreach (var roleName in roles.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var create = await roleManager.CreateAsync(new Role { Name = roleName });
                if (create.Succeeded)
                    _logger.LogInformation("Role '{Role}' created.", roleName);
                else
                    _logger.LogError("Failed to create role '{Role}': {Errors}", roleName, string.Join(", ", create.Errors.Select(e => e.Description)));
            }
        }
    }

    private async Task EnsureAdminAsync(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        SeedOptions.AdminOptions admin,
        CancellationToken ct)
    {
        var email = admin.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Seed:Admin:Email is empty. Skipping admin seeding.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(email);
        var hasAdminRoleConfigured = _opt.Roles.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            if (string.IsNullOrWhiteSpace(admin.Password))
            {
                _logger.LogWarning("Admin user not found and Seed:Admin:Password is empty. Skipping admin creation.");
                return;
            }

            var user = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = admin.FullName
            };

            var create = await userManager.CreateAsync(user, admin.Password!);
            if (!create.Succeeded)
            {
                _logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", create.Errors.Select(e => e.Description)));
                return;
            }

            _logger.LogInformation("Admin user created: {Email}", email);

            if (admin.EnsureAdminRole && hasAdminRoleConfigured)
            {
                var roleResult = await userManager.AddToRoleAsync(user, "Admin");
                if (!roleResult.Succeeded)
                    _logger.LogError("Failed to assign 'Admin' role: {Errors}", string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                else
                    _logger.LogInformation("Admin role assigned to {Email}.", email);
            }

            return;
        }

        // User tồn tại: cập nhật thông tin cơ bản
        var needUpdate = false;
        if (!string.IsNullOrWhiteSpace(admin.FullName) && admin.FullName != existing.FullName)
        {
            existing.FullName = admin.FullName;
            needUpdate = true;
        }
        if (!existing.EmailConfirmed) { existing.EmailConfirmed = true; needUpdate = true; }

        if (needUpdate)
        {
            var upd = await userManager.UpdateAsync(existing);
            if (!upd.Succeeded)
                _logger.LogError("Failed to update admin profile: {Errors}", string.Join(", ", upd.Errors.Select(e => e.Description)));
        }

        // Đảm bảo role Admin nếu cấu hình yêu cầu và role có trong danh sách Roles
        if (admin.EnsureAdminRole && hasAdminRoleConfigured && !await userManager.IsInRoleAsync(existing, "Admin"))
        {
            var assign = await userManager.AddToRoleAsync(existing, "Admin");
            if (!assign.Succeeded)
                _logger.LogError("Failed to add user to 'Admin' role: {Errors}", string.Join(", ", assign.Errors.Select(e => e.Description)));
            else
                _logger.LogInformation("Existing admin user added to 'Admin' role.");
        }

        // Reset mật khẩu nếu muốn và có cung cấp Password
        if (admin.ForceResetPassword && !string.IsNullOrWhiteSpace(admin.Password))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(existing);
            var reset = await userManager.ResetPasswordAsync(existing, token, admin.Password!);
            if (!reset.Succeeded)
                _logger.LogError("Failed to reset admin password: {Errors}", string.Join(", ", reset.Errors.Select(e => e.Description)));
            else
                _logger.LogInformation("Admin password reset.");
        }
    }
}
