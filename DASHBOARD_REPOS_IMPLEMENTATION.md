# Dashboard/Today Feature - DTOs & Repository Layer Implementation

## Summary

Successfully implemented the foundation for the Dashboard/Today feature following strict layering rules:
- ? DTOs project only
- ? Repositories.Interfaces and Repositories.WorkSeeds.Implements only
- ? NO references to Services/WebAPI from Repositories
- ? Reused existing BusinessObjects enums (EventMode, EventStatus, FriendStatus)
- ? NO new Result/Error types created

## Files Created

### A. DTOs Project (DTOs/)

#### 1. **DTOs/Dashboard/DashboardTodayDto.cs**
```csharp
public sealed record DashboardTodayDto(
    int Points,
    QuestTodayDto Quests,              // Reused from DTOs.Quests
    EventBriefDto[] EventsToday,
    ActivityDto Activity
);
```

#### 2. **DTOs/Dashboard/EventBriefDto.cs**
```csharp
public sealed record EventBriefDto(
    Guid Id,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string? Location,
    string Mode                         // Maps from EventMode enum
);
```

#### 3. **DTOs/Dashboard/ActivityDto.cs**
```csharp
public sealed record ActivityDto(
    int OnlineFriends,
    int QuestsDoneLast60m
);
```

### B. Repositories.Interfaces (Repositories/Interfaces/)

#### 4. **Repositories/Interfaces/IEventQueries.cs**
```csharp
public interface IEventQueries
{
    /// <summary>
    /// Get events starting within UTC range [startUtc, endUtc)
    /// Filters: Status != Draft/Canceled, soft-delete enabled
    /// Uses StartsAt index
    /// </summary>
    Task<IReadOnlyList<Event>> GetEventsStartingInRangeUtcAsync(
        DateTimeOffset startUtc, 
        DateTimeOffset endUtc, 
        CancellationToken ct = default);
}
```

#### 5. **Repositories/Interfaces/IFriendLinkQueries.cs**
```csharp
public interface IFriendLinkQueries
{
    /// <summary>
    /// Get list of accepted friend IDs for current user
    /// Returns the "other person" ID from FriendLink where Status=Accepted
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAcceptedFriendIdsAsync(
        Guid currentUserId, 
        CancellationToken ct = default);
}
```

### C. Repositories.WorkSeeds.Implements (Repositories/WorkSeeds/Implements/)

#### 6. **Repositories/WorkSeeds/Implements/EventQueries.cs**
Implementation highlights:
- ? Uses `AppDbContext` directly for read-only queries
- ? `.AsNoTracking()` for performance
- ? Filters `Status != Draft/Canceled`
- ? Query range on `StartsAt` index: `[startUtc, endUtc)`
- ? Soft-delete global filter automatically applied
- ? Selects only necessary fields (Id, Title, StartsAt, EndsAt, Location, Mode)
- ? NO Redis calls
- ? NO Services references

#### 7. **Repositories/WorkSeeds/Implements/FriendLinkQueries.cs**
Implementation highlights:
- ? Uses `AppDbContext` directly for read-only queries
- ? `.AsNoTracking()` for performance
- ? Returns only `Guid` list (no entity loading)
- ? Filters `Status == Accepted`
- ? Returns "other person" ID using conditional selection
- ? Soft-delete global filter automatically applied
- ? NO Redis calls
- ? NO Services references

## Implementation Details

### Query Optimization

**EventQueries**:
```csharp
var events = await _context.Events
    .AsNoTracking()
    .Where(e => e.StartsAt >= startUtc && e.StartsAt < endUtc)  // Uses index
    .Where(e => e.Status != EventStatus.Draft && e.Status != EventStatus.Canceled)
    .Select(e => new Event { ... })  // Project only needed fields
    .ToListAsync(ct);
```

**FriendLinkQueries**:
```csharp
var friendIds = await _context.FriendLinks
    .AsNoTracking()
    .Where(link => link.Status == FriendStatus.Accepted)
    .Where(link => link.SenderId == currentUserId || link.RecipientId == currentUserId)
    .Select(link => link.SenderId == currentUserId 
        ? link.RecipientId 
        : link.SenderId)  // Return "other person" ID
    .ToListAsync(ct);
```

## Layering Compliance ?

| Rule | Status |
|------|--------|
| DTOs in DTOs project | ? |
| BusinessObjects reused (EventMode, EventStatus, FriendStatus) | ? |
| NO new Result/Error types | ? |
| Repositories.Interfaces defined | ? |
| Repositories.WorkSeeds.Implements created | ? |
| Uses AppDbContext for queries | ? |
| Soft-delete filters respected | ? |
| NO Redis calls in Repositories | ? |
| NO Services/WebAPI references from Repositories | ? |
| StartsAt index usage | ? |
| Minimal data selection (no Include bloat) | ? |

## Next Steps (Not Implemented - Services Layer)

The following will need to be implemented in the **Services** layer:
- [ ] DashboardService implementation
- [ ] Redis integration for presence/counters (Services only)
- [ ] VN timezone calculations (Services.Common.Extensions.TimeExtensions)
- [ ] Aggregation of all data sources
- [ ] WebAPI controller endpoint

## Build Status

? **All files compile successfully**
? **No build errors**
? **No layer violations detected**

## Verification Commands

```bash
# Verify no Repositories references to Services
grep -r "using Services" Repositories/

# Verify no Redis in Repositories
grep -r "IConnectionMultiplexer\|StackExchange.Redis" Repositories/

# Build test
dotnet build Repositories/Repositories.csproj
dotnet build DTOs/DTOs.csproj
```

---

**Implementation Date**: 2025  
**Phase**: Dashboard/Today - DTOs & Repository Layer  
**Status**: ? Complete and verified
