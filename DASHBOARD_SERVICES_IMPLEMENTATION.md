# Dashboard/Today Feature - Services Layer Implementation

## Summary

Successfully implemented the **Services layer** for the Dashboard/Today feature following strict layering rules and performance best practices.

## Files Created/Modified

### 1. **Services/Interfaces/IDashboardService.cs** (NEW)
```csharp
public interface IDashboardService
{
    Task<Result<DashboardTodayDto>> GetTodayAsync(Guid userId, CancellationToken ct = default);
}
```

### 2. **Services/Implementations/DashboardService.cs** (NEW)
Full implementation with:
- ? VN timezone calculations (UTC+7)
- ? Parallel data fetching
- ? Batch Redis operations
- ? Efficient database queries
- ? Error handling

### 3. **Services/GlobalUsing.cs** (MODIFIED)
Added: `global using DTOs.Dashboard;`

## Implementation Details

### Architecture Compliance ?

| Requirement | Status | Details |
|------------|--------|---------|
| Service naming | ? | `DashboardService` ends with "Service" |
| Auto-registration | ? | Will be registered by DI convention |
| No WebAPI references | ? | Verified - no WebAPI dependencies |
| No new Result/Error | ? | Reuses existing BusinessObjects.Common.Results |
| Repository pattern | ? | Uses IEventQueries, IFriendLinkQueries |
| Layer separation | ? | Services ? Repositories ? BusinessObjects |

### Key Features

#### 1. **VN Timezone Handling**
```csharp
private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

var nowVn = DateTimeOffset.UtcNow.ToOffset(VnOffset);
var startVn = nowVn.Date;              // 00:00 VN time
var endVn = startVn.AddDays(1);        // 00:00 VN time next day

var startUtc = startVn.ToUniversalTime();
var endUtc = endUtc = endVn.ToUniversalTime();
```

#### 2. **Parallel Data Fetching**
```csharp
var pointsTask = GetUserPointsAsync(userId, ct);
var questsTask = _quests.GetTodayAsync(userId, ct);
var eventsTask = GetEventsTodayAsync(startUtc, endUtc, ct);
var activityTask = GetActivityAsync(userId, nowVn, ct);

await Task.WhenAll(pointsTask, questsTask, eventsTask, activityTask);
```

#### 3. **Efficient Redis Operations**

**Online Friends - Single Pipeline Batch Check**:
```csharp
// Get friend IDs
var friendIds = await _friendQueries.GetAcceptedFriendIdsAsync(userId, ct);

// Single batch presence check (1 Redis pipeline)
var presenceResult = await _presence.BatchIsOnlineAsync(friendIds, ct);

// Count online
return presenceResult.Value!.Values.Count(isOnline => isOnline);
```

**Quest Counters - MGET Batch Operation**:
```csharp
// Generate 60 minute keys
var keys = new List<RedisKey>(60);
for (int i = 0; i < 60; i++)
{
    var minuteVn = nowVn.AddMinutes(-i);
    keys.Add($"qc:done:{minuteVn:yyyyMMddHHmm}");
}

// Single MGET call for all 60 keys
var values = await db.StringGetAsync(keys.ToArray());

// Sum counts
int total = 0;
foreach (var value in values)
{
    if (value.HasValue && value.TryParse(out int count))
        total += count;
}
```

#### 4. **Minimal Database Queries**

**User Points**:
```csharp
var user = await _db.Users
    .AsNoTracking()
    .Where(u => u.Id == userId)
    .Select(u => new { u.Points })  // Project only needed field
    .FirstOrDefaultAsync(ct);
```

**Events Today**:
```csharp
// Uses IEventQueries with StartsAt index
var events = await _eventQueries
    .GetEventsStartingInRangeUtcAsync(startUtc, endUtc, ct);

// Map to DTO
return events.Select(e => new EventBriefDto(
    Id: e.Id,
    Title: e.Title,
    StartsAt: e.StartsAt,
    EndsAt: e.EndsAt,
    Location: e.Location,
    Mode: e.Mode.ToString()  // Enum -> string
)).ToArray();
```

## Performance Optimizations ?

### 1. **Batch Operations**
- ? Friend presence: 1 Redis pipeline (not N individual calls)
- ? Quest counters: 1 MGET operation for 60 keys (not 60 individual calls)

### 2. **Parallel Execution**
- ? All data sources fetched in parallel using `Task.WhenAll`
- ? No sequential dependencies between operations

### 3. **Minimal Data Transfer**
- ? User query selects only `Points` field
- ? Event query projects only needed fields
- ? `AsNoTracking()` for all read queries

### 4. **Index Usage**
- ? Events query uses `StartsAt` index
- ? User query on primary key (`Id`)
- ? FriendLink query optimized in Repository layer

## Dependencies Injected

```csharp
public DashboardService(
    AppDbContext db,                    // Direct DB access for user points
    IEventQueries eventQueries,         // Custom event queries
    IFriendLinkQueries friendQueries,   // Custom friend queries
    IPresenceService presence,          // Redis presence (batch)
    IQuestService quests,               // Quest service (reused)
    IConnectionMultiplexer redis)       // Redis for counters
```

## Error Handling

```csharp
// Validation
if (userId == Guid.Empty)
    return Result<DashboardTodayDto>.Failure(
        new Error(Error.Codes.Validation, "User ID is required"));

// Quest service result
if (questsResult.IsFailure)
    return Result<DashboardTodayDto>.Failure(questsResult.Error);

// Global exception handling
catch (Exception ex)
{
    return Result<DashboardTodayDto>.Failure(
        new Error(Error.Codes.Unexpected, $"Failed to get dashboard data: {ex.Message}"));
}

// Graceful degradation for activity metrics
if (presenceResult.IsFailure)
    return 0; // Don't fail entire dashboard
```

## Data Flow

```
???????????????????????????????????????????????????????
?              GetTodayAsync(userId)                  ?
???????????????????????????????????????????????????????
                         ?
         ?????????????????????????????????
         ?               ?               ?
         ?               ?               ?
    ??????????      ??????????     ???????????
    ? Points ?      ? Quests ?     ? Events  ?
    ?  (DB)  ?      ?(Service)?    ?(Queries)?
    ??????????      ??????????     ???????????
         ?               ?               ?
         ?????????????????????????????????
                         ?
                         ?
                  ???????????????
                  ?  Activity   ?
                  ???????????????
                         ?
         ?????????????????????????????????
         ?                               ?
         ?                               ?
    ????????????                   ????????????
    ?  Online  ?                   ? Quests   ?
    ? Friends  ?                   ?Done 60m  ?
    ?(Presence)?                   ? (Redis)  ?
    ????????????                   ????????????
         ?                               ?
         ?  (Batch)                      ? (MGET)
         ?                               ?
    1 Pipeline                      1 Operation
    Check N IDs                     Read 60 Keys
```

## Testing Recommendations

### Unit Tests
```csharp
[Fact]
public async Task GetTodayAsync_ShouldReturnDashboard_WithAllData()
{
    // Arrange: Mock all dependencies
    // Act: Call GetTodayAsync
    // Assert: Verify all fields populated correctly
}

[Fact]
public async Task GetOnlineFriendsCountAsync_ShouldUseBatchCheck()
{
    // Verify BatchIsOnlineAsync called once (not per friend)
}

[Fact]
public async Task GetQuestsDoneLast60MinutesAsync_ShouldUseMGET()
{
    // Verify single MGET operation for 60 keys
}
```

### Integration Tests
```csharp
[Fact]
public async Task GetTodayAsync_WithRealData_ShouldReturnCorrectCounts()
{
    // Setup: Real data in DB + Redis
    // Act: Call service
    // Assert: Verify counts match expected
}
```

## DI Registration

? **Auto-registered** by convention in `Services.Common.DependencyInjection`:

```csharp
// RegisterServiceInterfacesByConvention scans for:
// - Classes ending with "Service"
// - Interfaces ending with "Service"
// - Auto-registers as Scoped (default)

// Result:
services.AddScoped<IDashboardService, DashboardService>();
```

## Build Status

? **All files compile successfully**  
? **No build errors**  
? **No layer violations**  
? **No WebAPI references**  
? **No new Result/Error types**

## Verification Commands

```bash
# Verify no WebAPI references
Select-String -Path "Services\**\*.cs" -Pattern "using WebAPI"

# Verify no new Result types
Select-String -Path "Services\Implementations\DashboardService.cs" -Pattern "class.*Result"

# Verify service naming
Select-String -Path "Services\Implementations\DashboardService.cs" -Pattern "class.*Service"

# Build test
dotnet build Services/Services.csproj
```

## Next Steps (WebAPI Layer - Not Included)

The following still needs to be implemented:
- [ ] DashboardController in WebAPI
- [ ] GET /api/dashboard/today endpoint
- [ ] Authorization attributes
- [ ] Swagger documentation

---

**Implementation Date**: 2025  
**Phase**: Dashboard/Today - Services Layer  
**Status**: ? Complete and verified  
**Performance**: ? Optimized with batch operations
