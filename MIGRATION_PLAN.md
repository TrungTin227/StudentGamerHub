# Migration Plan: Membership Tables

## Summary
Introduce membership tables for communities and clubs so the membership hierarchy can persist ownership and membership roles consistently. Existing room membership schema remains unchanged.

## Required Schema Changes
1. **Table `community_members`** (composite primary key `(CommunityId, UserId)`)
   - Columns:
     - `CommunityId` (`uuid`, not null) FK → `communities.Id` (cascade delete)
     - `UserId` (`uuid`, not null) FK → `users.Id` (cascade delete)
     - `Role` (`text`, not null) storing `CommunityRole` as string (`Owner`, `Mod`, `Member`)
     - `JoinedAt` (`timestamp without time zone`, not null)
   - Indexes:
     - Unique primary key `(CommunityId, UserId)`
     - Non-clustered index on `UserId` for reverse lookups.

2. **Table `club_members`** (composite primary key `(ClubId, UserId)`)
   - Columns:
     - `ClubId` (`uuid`, not null) FK → `clubs.Id` (cascade delete)
     - `UserId` (`uuid`, not null) FK → `users.Id` (cascade delete)
     - `Role` (`text`, not null) storing `CommunityRole` as string (`Owner`, `Mod`, `Member`)
     - `JoinedAt` (`timestamp without time zone`, not null)
   - Indexes:
     - Unique primary key `(ClubId, UserId)`
     - Non-clustered index on `UserId` for querying memberships per user.

3. **Seed / Data considerations**
   - No data backfill is required; ownership members will be inserted by application logic.

Apply these changes via `dotnet ef migrations add <Name>` targeting the `Repositories` project, then update the database. No existing tables are modified.

---

## Payments Safeguards (Wallet Top-Up Enablement & Idempotency)

The following schema updates must be applied before enabling wallet top-ups in production environments.

### SQL Server
```
ALTER TABLE [payment_intents] DROP CONSTRAINT [chk_payment_intent_purpose_allowed];
ALTER TABLE [payment_intents] ADD CONSTRAINT [chk_payment_intent_purpose_allowed] CHECK ([Purpose] IN ('TopUp','EventTicket','WalletTopUp'));
CREATE UNIQUE INDEX [IX_transactions_provider_providerref] ON [transactions]([Provider],[ProviderRef]) WHERE [ProviderRef] IS NOT NULL;
```

### PostgreSQL
```
ALTER TABLE payment_intents DROP CONSTRAINT IF EXISTS chk_payment_intent_purpose_allowed;
ALTER TABLE payment_intents ADD CONSTRAINT chk_payment_intent_purpose_allowed CHECK ("Purpose" IN ('TopUp','EventTicket','WalletTopUp'));
CREATE UNIQUE INDEX IF NOT EXISTS ix_transactions_provider_providerref ON transactions("Provider","ProviderRef") WHERE "ProviderRef" IS NOT NULL;
```

These updates widen the payment-intent purpose check constraint to include wallet top-ups and ensure provider callbacks cannot create duplicate transaction rows.
