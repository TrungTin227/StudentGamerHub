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
