# Wallet Architecture - One Wallet Per User

## Overview

This document describes the wallet mechanism in StudentGamerHub and how the system enforces the critical **one-wallet-per-user** invariant.

## Core Principle

**Each user has exactly ONE personal wallet** tied to their account (`userId`). The system prevents multiple wallets per user through multiple layers of protection.

## Enforcement Mechanisms

### 1. Database Layer (Primary Protection)

**Location**: `Repositories/Persistence/AppDbContext.cs:462`

```csharp
b.Entity<Wallet>(e =>
{
    e.HasOne(x => x.User).WithOne(x => x.Wallet!)
     .HasForeignKey<Wallet>(x => x.UserId)
     .OnDelete(DeleteBehavior.Cascade);

    e.HasIndex(x => x.UserId).IsUnique();  // ← CRITICAL: Prevents multiple wallets

    e.HasCheckConstraint(
        "chk_wallet_balance_nonneg",
        isNpgsql ? "\"BalanceCents\" >= 0" : "[BalanceCents] >= 0");
});
```

**Key Features**:
- Unique index on `UserId` column prevents duplicate wallet creation at database level
- One-to-One relationship between `User` and `Wallet`
- Any attempt to insert a second wallet for the same user will fail with constraint violation

### 2. Repository Layer (Application Logic Protection)

**Location**: `Repositories/Implements/WalletRepository.cs`

**Method**: `CreateIfMissingAsync(Guid userId, CancellationToken ct)`

```csharp
public async Task CreateIfMissingAsync(Guid userId, CancellationToken ct = default)
{
    // Check if wallet already exists
    var exists = await _context.Wallets
        .AsNoTracking()
        .AnyAsync(w => w.UserId == userId, ct)
        .ConfigureAwait(false);

    if (exists)
    {
        _logger.LogDebug("Wallet already exists for user {UserId}, skipping creation...");
        return;  // ← Exit early if wallet exists
    }

    // Create new wallet only if none exists
    var wallet = new Wallet { UserId = userId };
    await _context.Wallets.AddAsync(wallet, ct).ConfigureAwait(false);
    _logger.LogInformation("Created new wallet for user {UserId}...");
}
```

**Protection Strategy**:
1. Query database to check if wallet exists for user
2. If exists, return immediately without creating
3. If not exists, create new wallet
4. Database unique constraint provides final safety net

### 3. Service Layer (Business Logic Integration)

All wallet-related operations use `CreateIfMissingAsync` before performing operations:

#### PaymentService

- **ConfirmEventTicketAsync** (line 273): Creates wallet before debiting for ticket purchase
- **ConfirmTopUpAsync** (line 369): Creates wallet before debiting for escrow top-up
- **CreateWalletTopUpIntentAsync** (line 154): Creates wallet before creating top-up payment intent

#### EventService

- **CreateAsync** (line 105-106):
  - Creates organizer wallet before charging event creation fee
  - Creates platform wallet if needed (via `IPlatformAccountService`)
- **CancelAsync** (line 298): Creates attendee wallets before issuing refunds

#### WalletReadService

- **GetAsync** (line 24): Creates wallet on-demand when user queries their balance
- **GetPlatformWalletAsync** (line 38): Creates platform wallet on-demand

## Platform Wallet (Special Case)

The **platform wallet** is a special wallet owned by a system account, used for:
- Collecting event creation fees
- Platform revenue from ticket sales
- System-level financial operations

**Resolution**: Always resolved through `IPlatformAccountService.GetOrCreatePlatformUserIdAsync()`

**Location**: `Services/Implementations/PlatformAccountService.cs`

**Key Features**:
- Thread-safe creation with `SemaphoreSlim` locking
- Deterministic user ID or email-based lookup
- Cached after first resolution
- Separate from regular user wallets

## Wallet Creation Flow

```
User Action (e.g., GET /api/wallet)
    ↓
[Authorize] - Verify user identity
    ↓
WalletReadService.GetAsync(userId)
    ↓
WalletRepository.CreateIfMissingAsync(userId)
    ↓
Check: Does wallet exist for userId?
    ├─ YES → Return immediately (no-op)
    └─ NO → Create new wallet
            ↓
            Database unique index ensures one wallet
            ↓
            Log wallet creation
            ↓
            Return wallet
```

## Critical Implementation Points

### ✅ Always Call CreateIfMissingAsync

Before any wallet operation (query, balance adjustment, transaction), call:

```csharp
await _walletRepository.CreateIfMissingAsync(userId, ct);
await _uow.SaveChangesAsync(ct);
```

### ✅ Never Allow Manual Wallet Creation

- No public API endpoints for creating wallets
- Wallets are created implicitly on first use
- Guests (unauthenticated users) cannot trigger wallet creation

### ✅ Platform Wallet via IPlatformAccountService

Always resolve platform wallet through the service:

```csharp
var platformUserResult = await _platformAccountService
    .GetOrCreatePlatformUserIdAsync(ct);

if (platformUserResult.IsSuccess)
{
    var platformUserId = platformUserResult.Value;
    await _walletRepository.CreateIfMissingAsync(platformUserId, ct);
}
```

## Logging and Monitoring

The system logs wallet operations for audit and debugging:

- **DEBUG**: Wallet already exists messages (skipped creation)
- **INFO**: New wallet creation events
- **ERROR**: Wallet creation/retrieval failures

**Example Logs**:
```
[INFO] Created new wallet for user a1b2c3d4-..., One-wallet-per-user invariant protected by unique index
[DEBUG] Wallet already exists for user e5f6g7h8-..., skipping creation to maintain invariant
```

## Verification

### Database Constraint Verification

Run this SQL query to verify the unique constraint:

```sql
-- PostgreSQL
SELECT COUNT(*), "UserId"
FROM wallets
GROUP BY "UserId"
HAVING COUNT(*) > 1;
-- Should return 0 rows (no duplicate wallets)

-- Check constraint existence
SELECT conname, contype
FROM pg_constraint
WHERE conrelid = 'wallets'::regclass;
```

### Code Review Checklist

When adding new wallet-related features:

- [ ] Does the code call `CreateIfMissingAsync` before wallet operations?
- [ ] Is the userId obtained from authenticated user context?
- [ ] Is platform wallet resolved via `IPlatformAccountService`?
- [ ] Are wallet creation operations logged appropriately?
- [ ] Does the code handle wallet-not-found scenarios gracefully?

## Files Modified (Current Implementation)

1. **Repositories/Interfaces/IWalletRepository.cs** - Added XML documentation
2. **Repositories/Implements/WalletRepository.cs** - Implemented `EnsureAsync` and `Detach`, added logging
3. **Services/Implementations/WalletReadService.cs** - Added `CreateIfMissingAsync` call
4. **Services/Implementations/PaymentService.cs** - Added `CreateIfMissingAsync` in `CreateWalletTopUpIntentAsync`
5. **Repositories/Persistence/AppDbContext.cs** - Unique index on UserId (existing)

## Testing Recommendations

### Unit Tests

```csharp
[Fact]
public async Task CreateIfMissingAsync_WhenWalletExists_ShouldNotCreateDuplicate()
{
    // Arrange
    var userId = Guid.NewGuid();
    await _walletRepository.CreateIfMissingAsync(userId);
    await _context.SaveChangesAsync();

    // Act
    await _walletRepository.CreateIfMissingAsync(userId);
    await _context.SaveChangesAsync();

    // Assert
    var count = await _context.Wallets.CountAsync(w => w.UserId == userId);
    Assert.Equal(1, count);
}

[Fact]
public async Task CreateWallet_WithDuplicateUserId_ShouldThrowException()
{
    // Arrange
    var userId = Guid.NewGuid();
    var wallet1 = new Wallet { UserId = userId };
    var wallet2 = new Wallet { UserId = userId };

    await _context.Wallets.AddAsync(wallet1);
    await _context.SaveChangesAsync();

    // Act & Assert
    await _context.Wallets.AddAsync(wallet2);
    await Assert.ThrowsAsync<DbUpdateException>(
        () => _context.SaveChangesAsync());
}
```

### Integration Tests

```csharp
[Fact]
public async Task GetWallet_ForNewUser_ShouldCreateWalletAutomatically()
{
    // Arrange
    var userId = CreateAuthenticatedUser();

    // Act
    var response = await _client.GetAsync($"/api/wallet");

    // Assert
    response.EnsureSuccessStatusCode();
    var wallet = await response.Content.ReadFromJsonAsync<WalletSummaryDto>();
    Assert.True(wallet.Exists);
    Assert.Equal(0, wallet.BalanceCents);
}
```

## Summary

The one-wallet-per-user invariant is enforced through:

1. **Database unique index** on `Wallet.UserId` (hard constraint)
2. **Repository existence check** in `CreateIfMissingAsync` (soft constraint)
3. **Service-level integration** ensuring all operations call `CreateIfMissingAsync`
4. **Logging and monitoring** for audit trail
5. **Platform wallet isolation** via dedicated service

This multi-layered approach ensures data integrity and prevents wallet duplication under all circumstances.
