# Teammates Search Service Layer Implementation

## Overview
This implementation provides the service layer for teammate search functionality, enriching repository results with real-time presence information and applying online-first sorting.

---

## Architecture Compliance

### ? REPO ORDER (mandatory)
- **Project:** Services only
- **Dependencies:** BusinessObjects, DTOs, Repositories
- **NO** references to WebAPI from Services

### ? BASE REUSE (mandatory)
- Uses `BusinessObjects.Common.Results` (Result<T>)
- Uses `BusinessObjects.Common.Pagination` (CursorRequest/CursorPageResult)
- Uses `Services.Common.Mapping` (User?UserBriefDto pattern)
- Uses `IPresenceService.BatchIsOnlineAsync` (1 pipeline)
- **NO** new Result/Error types created

### ? HARD CONSTRAINTS
1. ? **OnlineOnly** uses `IPresenceService.BatchIsOnlineAsync` (1 Redis pipeline, no loops)
2. ? **Sort Order (MVP):** online DESC ? points DESC ? sharedGames DESC ? userId DESC
   - Repository sorts by (points, shared, id)
   - Service adds "online" priority at the beginning
3. ? Returns `Result<CursorPageResult<TeammateDto>>`

---

## Implementation Details

### Interface: `ITeammateFinderService`

**Location:** `Services/Interfaces/ITeammateFinderService.cs`

```csharp
public interface ITeammateFinderService
{
    Task<Result<CursorPageResult<DTOs.Teammates.TeammateDto>>> SearchAsync(
        Guid currentUserId,
        Guid? gameId,
        string? university,
        GameSkillLevel? skill,
        bool onlineOnly,
        CursorRequest cursor,
        CancellationToken ct = default);
}
```

**Parameters:**
- `currentUserId`: User performing the search (excluded from results)
- `gameId`: Optional game filter
- `university`: Optional university filter  
- `skill`: Optional skill level filter
- `onlineOnly`: If true, only return currently online users
- `cursor`: Cursor-based pagination request
- `ct`: Cancellation token

**Returns:** `Result<CursorPageResult<TeammateDto>>`

---

### Implementation: `TeammateFinderService`

**Location:** `Services/Implementations/TeammateFinderService.cs`

#### Dependencies
- `ITeammateQueries`: Repository queries (from Repositories project)
- `IPresenceService`: Redis presence check (batch operations)

#### Pipeline (6 Steps)

**Step 1: Call Repository**
```csharp
var filter = new TeammateSearchFilter(gameId, university, skill);
var (candidates, nextCursor) = await _teammateQueries
    .SearchCandidatesAsync(currentUserId, filter, cursor, ct);
```
- Repository returns minimal projection with SharedGames calculated
- Already sorted by: points DESC, sharedGames DESC, userId DESC

**Step 2: Batch Presence Check (1 Pipeline)**
```csharp
var userIds = candidates.Select(c => c.UserId).ToArray();
var presenceResult = await _presence.BatchIsOnlineAsync(userIds, ct);
```
- **Single Redis pipeline** for all user IDs
- No loops, no individual checks
- Returns `Dictionary<Guid, bool>`

**Step 3: Map to DTOs**
```csharp
var items = candidates.Select(c => new
{
    Dto = new TeammateDto
    {
        User = new UserBriefDto(
            Id: c.UserId,
            UserName: c.FullName ?? c.UserId.ToString(),
            AvatarUrl: c.AvatarUrl
        ),
        IsOnline = onlineMap.TryGetValue(c.UserId, out var online) && online,
        SharedGames = c.SharedGames
    },
    Points = c.Points,
    UserId = c.UserId
}).ToList();
```
- Maps `TeammateCandidate` ? `TeammateDto`
- Enriches with `IsOnline` from presence check
- Keeps sorting fields (Points, UserId) for next step

**Step 4: Filter by OnlineOnly**
```csharp
if (onlineOnly)
{
    items = items.Where(x => x.Dto.IsOnline).ToList();
}
```
- Applied after mapping
- Simple LINQ filter in memory (already small result set from pagination)

**Step 5: Sort with Online Priority**
```csharp
var sorted = items
    .OrderByDescending(x => x.Dto.IsOnline)  // Online first
    .ThenByDescending(x => x.Points)          // Then by points
    .ThenByDescending(x => x.Dto.SharedGames) // Then by shared games
    .ThenByDescending(x => x.UserId)          // Finally by user ID
    .Select(x => x.Dto)
    .ToList();
```
- **Sort order:** online DESC ? points DESC ? sharedGames DESC ? userId DESC
- Repository provided base sort (points/shared/id)
- Service adds online status as primary sort key

**Step 6: Wrap in CursorPageResult**
```csharp
var result = new CursorPageResult<TeammateDto>(
    Items: sorted,
    NextCursor: nextCursor,
    PrevCursor: null,
    Size: cursor.SizeSafe,
    Sort: cursor.SortSafe,
    Desc: cursor.Desc
);
```
- Uses `nextCursor` from repository
- Note: Cursor is based on (points, shared, id) only, not online status

---

## Cursor Jitter Explanation

### The Issue
The cursor returned by the repository is based on **stable database fields**:
- `points:sharedGames:userId`

However, the **final sort order** includes `IsOnline` (ephemeral Redis data):
- `online DESC ? points DESC ? sharedGames DESC ? userId DESC`

### Example Scenario
1. **Page 1 request (time T0):**
   - User A: online=true, points=100
   - User B: online=false, points=150
   - **Sorted result:** A (online 100), B (offline 150)
   - **Cursor:** Based on B's (points:150, shared, id)

2. **Page 2 request (time T1):**
   - User A goes offline
   - User B comes online
   - Cursor still points to "points < 150"
   - But now B would sort *before* items with points < 150 (because online=true)

### Why It's Acceptable (MVP)
- Online status is **real-time ephemeral data**
- Users expect some inconsistency in "online" features
- Alternatives (session-locked snapshots, complex cursor encoding) add significant complexity
- Documented in code comments as expected behavior

### Future Improvement (Post-MVP)
If needed, could implement:
- Cursor includes online status: `"online:points:shared:id"`
- Service maintains a short-lived snapshot of online status per search session
- Trade-off: More complexity, potentially stale online data

---

## Dependency Injection

### Automatic Registration
The service is **automatically registered** by convention in `DependencyInjection.cs`:

```csharp
private static void RegisterServiceInterfacesByConvention(IServiceCollection services, Assembly asm)
{
    var impls = asm.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Service"));

    foreach (var impl in impls)
    {
        var interfaces = impl.GetInterfaces()
            .Where(i => i.Name.EndsWith("Service"));
        
        // Default: Scoped lifetime
        foreach (var itf in interfaces)
        {
            services.Add(new ServiceDescriptor(itf, impl, ServiceLifetime.Scoped));
        }
    }
}
```

**Result:**
- `ITeammateFinderService` ? `TeammateFinderService` (Scoped)
- No manual registration needed

---

## Testing Recommendations

### Unit Tests

**1. Empty Results**
```csharp
// Repository returns empty list
// Should return empty CursorPageResult, not fail
```

**2. Presence Service Failure**
```csharp
// BatchIsOnlineAsync returns failure
// Should propagate error as Result<T>.Failure
```

**3. OnlineOnly Filter**
```csharp
// Given: 3 online, 2 offline candidates
// When: onlineOnly = true
// Then: Only 3 online returned
```

**4. Sort Order Verification**
```csharp
// Given: Mixed online/offline users with various points
// When: SearchAsync called
// Then: Verify order is online DESC ? points DESC ? shared DESC ? id DESC
```

**5. Single Pipeline Verification**
```csharp
// Verify BatchIsOnlineAsync called exactly once
// Verify no loops calling IsOnlineAsync individually
```

### Integration Tests

**1. Full Pipeline**
```csharp
// Real repository + real Redis
// Verify end-to-end sorting works
// Verify pagination cursor works
```

**2. Cursor Consistency**
```csharp
// Page 1 ? Page 2 ? Page 3
// Verify no duplicate or missing results (when online status stable)
```

**3. Performance**
```csharp
// 100 candidates
// Verify single Redis batch call (not 100 individual calls)
```

---

## Performance Characteristics

### Repository Query
- **Indexed fields:** User.University, UserGame.GameId, UserGame.Skill
- **Complexity:** O(log n) index seek + O(m) result set (m = page size)
- **Expected:** < 50ms for typical queries

### Presence Check
- **Single Redis pipeline:** O(m) where m = candidate count
- **Network:** 1 round-trip to Redis
- **Expected:** < 10ms for m ? 20

### In-Memory Operations
- **Mapping:** O(m)
- **Filtering (onlineOnly):** O(m)
- **Sorting:** O(m log m) — small m (page size) makes this negligible
- **Expected:** < 5ms for m = 20

### Total
- **Target:** < 100ms for typical query (20 results)
- **Breakdown:**
  - Repository: 50ms
  - Presence: 10ms
  - Service logic: 5ms
  - Network/overhead: 35ms

---

## Files Created/Modified

### Created
1. `Services/Interfaces/ITeammateFinderService.cs`
2. `Services/Implementations/TeammateFinderService.cs`

### Modified
- **None** (convention-based DI handles registration automatically)

---

## Build & Validation

```bash
dotnet build Services/Services.csproj
```

**Status:** ? Build successful

---

## Grep Protection Checks

### ? No New Result/Error Types
```bash
grep -r "class.*Result" Services/Implementations/TeammateFinderService.cs
# No new Result/Error classes defined
```

### ? No WebAPI References
```bash
grep -r "using.*WebAPI" Services/
# No matches - Services doesn't reference WebAPI
```

### ? Single Presence Pipeline
```csharp
// TeammateFinderService.cs:Line 68
await _presence.BatchIsOnlineAsync(userIds, ct);
// Only one call - no loops
```

### ? Sort Order Correct
```csharp
// TeammateFinderService.cs:Lines 94-98
.OrderByDescending(x => x.Dto.IsOnline)     // ? Online first
.ThenByDescending(x => x.Points)            // ? Then points
.ThenByDescending(x => x.Dto.SharedGames)   // ? Then shared games
.ThenByDescending(x => x.UserId)            // ? Then user ID
```

---

## Summary

? **Interface:** `ITeammateFinderService` with SearchAsync method  
? **Implementation:** `TeammateFinderService` with 6-step pipeline  
? **DI Registration:** Automatic (convention-based)  
? **Sort Order:** online DESC ? points DESC ? sharedGames DESC ? userId DESC  
? **Presence:** Single batch call (no loops)  
? **Return Type:** `Result<CursorPageResult<TeammateDto>>`  
? **Architecture:** No WebAPI dependency, reuses existing infrastructure  
? **Build:** Successful  

**Cursor jitter** documented and accepted as MVP behavior for real-time online status.

The service layer implementation is **complete and ready for WebAPI integration**.
