using System.Linq;
using System.Threading;
using BusinessObjects;
using BusinessObjects.Common.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Configuration;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>
/// Provides access to the platform user that owns the platform wallet. If a configuration value is not supplied the
/// service will create (or reuse) a deterministic account based on the configured e-mail.
/// </summary>
public sealed class PlatformAccountService : IPlatformAccountService
{
    private readonly UserManager<User> _userManager;
    private readonly IOptionsMonitor<BillingOptions> _billingOptions;
    private readonly ILogger<PlatformAccountService> _logger;
    private static readonly SemaphoreSlim InitializationLock = new(1, 1);
    private static Guid? _cachedUserId;

    public PlatformAccountService(
        UserManager<User> userManager,
        IOptionsMonitor<BillingOptions> billingOptions,
        ILogger<PlatformAccountService> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _billingOptions = billingOptions ?? throw new ArgumentNullException(nameof(billingOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<Guid>> GetOrCreatePlatformUserIdAsync(CancellationToken ct = default)
    {
        var cached = _cachedUserId;
        if (cached.HasValue)
        {
            return Result<Guid>.Success(cached.Value);
        }

        var options = _billingOptions.CurrentValue ?? new BillingOptions();

        if (options.PlatformUserId.HasValue && options.PlatformUserId.Value != Guid.Empty)
        {
            var ensured = await EnsureConfiguredUserAsync(options.PlatformUserId.Value, options, ct).ConfigureAwait(false);
            if (ensured.IsSuccess)
            {
                _cachedUserId = ensured.Value;
            }
            return ensured;
        }

        var auto = await EnsureAutoUserAsync(options, ct).ConfigureAwait(false);
        if (auto.IsSuccess)
        {
            _cachedUserId = auto.Value;
        }
        return auto;
    }

    private async Task<Result<Guid>> EnsureConfiguredUserAsync(Guid userId, BillingOptions options, CancellationToken ct)
    {
        var existing = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (existing is not null)
        {
            return Result<Guid>.Success(existing.Id);
        }

        await InitializationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            existing = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
            if (existing is not null)
            {
                return Result<Guid>.Success(existing.Id);
            }

            return await CreatePlatformUserAsync(userId, options, ct).ConfigureAwait(false);
        }
        finally
        {
            InitializationLock.Release();
        }
    }

    private async Task<Result<Guid>> EnsureAutoUserAsync(BillingOptions options, CancellationToken ct)
    {
        var email = string.IsNullOrWhiteSpace(options.PlatformUserEmail)
            ? "platform@studentgamerhub.local"
            : options.PlatformUserEmail.Trim();

        var existing = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (existing is not null)
        {
            return Result<Guid>.Success(existing.Id);
        }

        await InitializationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check once inside the critical section to avoid race conditions.
            existing = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
            if (existing is not null)
            {
                return Result<Guid>.Success(existing.Id);
            }

            return await CreatePlatformUserAsync(Guid.NewGuid(), options, ct, email).ConfigureAwait(false);
        }
        finally
        {
            InitializationLock.Release();
        }
    }

    private async Task<Result<Guid>> CreatePlatformUserAsync(Guid userId, BillingOptions options, CancellationToken ct, string? emailOverride = null)
    {
        var email = string.IsNullOrWhiteSpace(emailOverride)
            ? (string.IsNullOrWhiteSpace(options.PlatformUserEmail)
                ? $"platform+{userId:N}@studentgamerhub.local"
                : options.PlatformUserEmail.Trim())
            : emailOverride;

        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = userId,
            UserName = BuildUserName(email, userId),
            Email = email,
            EmailConfirmed = true,
            FullName = string.IsNullOrWhiteSpace(options.PlatformUserName)
                ? "Platform Wallet"
                : options.PlatformUserName,
            CreatedAtUtc = now,
            CreatedBy = userId,
            IsDeleted = false,
            LockoutEnabled = false,
        };

        var result = await _userManager.CreateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errorDescriptions = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError(
                "Failed to create platform user {PlatformUserId}: {Errors}",
                userId,
                errorDescriptions);

            return Result<Guid>.Failure(new Error(Error.Codes.Unexpected, "Failed to create platform user."));
        }

        _logger.LogInformation("Platform user ensured with id {PlatformUserId}.", userId);
        return Result<Guid>.Success(user.Id);
    }

    private static string BuildUserName(string email, Guid userId)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            return email;
        }

        return $"platform-{userId:N}";
    }
}
