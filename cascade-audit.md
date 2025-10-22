# Cascade Audit Report

## Summary
- Identified that the development `AppSeeder` was enriching every community/club/room in the database regardless of origin, which caused bulk child creation whenever a new aggregate was inserted during a seeding run. 【F:Repositories/Persistence/Seeding/AppSeeder.cs†L426-L691】
- Added guard clauses so the seeder now scopes itself to seed-owned rows (`CreatedBy == null`) and leaves user-created entities untouched. 【F:Repositories/Persistence/Seeding/AppSeeder.cs†L428-L609】
- Introduced integration tests that simulate the create flows, execute the seeder afterwards, and assert that no unintended children appear. 【F:Tests/Services.Communities.Tests/CascadeSeedingGuardTests.cs†L23-L214】

## Reproduction
1. Start the WebAPI with seeding enabled (default `Seed:Run = true`).
2. Issue `POST /api/communities` while the hosted `DbInitializerHostedService` is still executing `AppSeeder.SeedAsync`.
3. Observe that the community obtains 1-3 random clubs because `SeedClubsAsync` pulls every community from `_db.Communities` without filtering. 【F:Repositories/Persistence/Seeding/AppSeeder.cs†L480-L531】
4. Repeat for `POST /api/clubs` or `POST /api/rooms`; `SeedRoomsAsync` and `SeedRoomMembersAsync` likewise attach rooms and members to the just-created records. 【F:Repositories/Persistence/Seeding/AppSeeder.cs†L540-L691】

## Root Causes
1. **Seeder lacks ownership filter** – `SeedCommunityGamesAsync`, `SeedClubsAsync`, `SeedRoomsAsync`, and `SeedRoomMembersAsync` select *all* rows, so user-created parents are enriched with sample data whenever the seeder runs. 【F:Repositories/Persistence/Seeding/AppSeeder.cs†L428-L609】
2. **Seeder executed during live traffic** – `DbInitializerHostedService` runs on startup; requests made before completion enter the same transaction scope, exposing the above logic. (No code change required once filtering is applied.)

## Fixes
- Filter every seeding phase that mutates child collections to `CreatedBy == null`, which is true for seed data but false for runtime creations. 【F:Repositories/Persistence/Seeding/AppSeeder.cs†L428-L609】
- Leave existing indexes intact; they already enforce uniqueness for clubs, rooms, and memberships. 【F:Repositories/Persistence/AppDbContext.cs†L272-L354】
- Added regression tests covering community, club, and room create flows with a post-create seeder execution. 【F:Tests/Services.Communities.Tests/CascadeSeedingGuardTests.cs†L23-L214】

## Database Constraints
- Clubs enforce `UNIQUE(CommunityId, Name)`. 【F:Repositories/Persistence/AppDbContext.cs†L276-L287】
- Rooms enforce `UNIQUE(ClubId, Name)`. 【F:Repositories/Persistence/AppDbContext.cs†L305-L327】
- Room members enforce `UNIQUE(RoomId, UserId)` and supporting status indexes. 【F:Repositories/Persistence/AppDbContext.cs†L329-L354】

## Tests Added
- `CascadeSeedingGuardTests.CreateCommunity_ShouldNotGainSeederClubs`.
- `CascadeSeedingGuardTests.CreateClub_ShouldNotGainSeederRooms`.
- `CascadeSeedingGuardTests.CreateRoom_ShouldNotGainSeederMembers`. 【F:Tests/Services.Communities.Tests/CascadeSeedingGuardTests.cs†L23-L214】

## Verification
- .NET SDK is not available in the execution environment, so automated test execution was not possible. Manual code reasoning and the new tests capture the regression scenarios for future runs.
