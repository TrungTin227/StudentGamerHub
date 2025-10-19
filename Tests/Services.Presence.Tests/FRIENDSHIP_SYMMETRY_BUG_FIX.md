# Friendship Symmetry Bug Fix

## Bug Description
**"M?t bên là b?n, bên kia ch?a"** - "One side shows as friends, the other side doesn't"

### Symptoms
When User A sends a friend request to User B:
- ? User A sees User B with `IsPending = true` (correct)
- ? User B sees User A with `IsPending = false` (WRONG - should be `true`)

After User B accepts the request:
- ? Both sides should see `IsFriend = true, IsPending = false`
- ? **BUG**: One side might still show incorrect pending state

## Root Cause

Located in `Services/Friends/FriendService.cs`, method `SearchUsersAsync` (around line 71-76):

```csharp
// ? BUGGY CODE (original):
var isPending = link?.Status == FriendStatus.Pending && link.SenderId == currentUserId;
```

### Problem
This condition only returns `true` when **the current user is the sender** (outgoing request). It completely ignores **incoming requests** where the current user is the recipient.

### Database State
The `FriendLink` table has a directional design:
- `SenderId`: The user who sent the request
- `RecipientId`: The user who received the request  
- `Status`: `Pending`, `Accepted`, `Declined`

When A invites B:
```
SenderId: A
RecipientId: B
Status: Pending
```

The buggy query would only show `IsPending = true` when searching as user A, but not when searching as user B.

## The Fix

### Code Change
**File**: `Services/Friends/FriendService.cs`  
**Method**: `SearchUsersAsync`  
**Lines**: ~71-76

```csharp
// ? FIXED CODE:
var items = pagedUsers.Items
    .Select(user =>
    {
        var link = relatedLinks.FirstOrDefault(l =>
            (l.SenderId == currentUserId && l.RecipientId == user.Id) ||
            (l.RecipientId == currentUserId && l.SenderId == user.Id));

        // ? FIX: Check both directions for IsFriend (bidirectional relationship)
        var isFriend = link?.Status == FriendStatus.Accepted;
        
        // ? FIX: IsPending should be true for BOTH outgoing and incoming requests
        // This fixes the "m?t bên là b?n, bên kia ch?a" bug
        var isPending = link?.Status == FriendStatus.Pending;

        return user.ToUserSearchItemDto(isFriend, isPending);
    })
    .ToList();
```

### What Changed
**Before**:
```csharp
var isPending = link?.Status == FriendStatus.Pending && link.SenderId == currentUserId;
```

**After**:
```csharp
var isPending = link?.Status == FriendStatus.Pending;
```

### Why This Works
By removing the `&& link.SenderId == currentUserId` check, the `isPending` flag now correctly returns `true` for **both** parties in a pending friendship:

1. **Outgoing request** (User A invites User B):
   - A searches B: `link.SenderId == A`, `link.Status == Pending` ? `isPending = true` ?
   - B searches A: `link.RecipientId == B`, `link.Status == Pending` ? `isPending = true` ?

2. **After acceptance** (User B accepts):
   - Both sides: `link.Status == Accepted` ? `isFriend = true, isPending = false` ?

## Testing Strategy

### Integration Tests Created
**File**: `Tests/Services.Presence.Tests/Friendship_Symmetry_Tests.cs`

#### Test Cases
1. **`SearchUsers_AfterAccept_BothSidesShouldSeeFriendStatus`**
   - Verifies complete flow: invite ? pending check (both sides) ? accept ? friend check (both sides)
   - **Critical assertion**: After accept, both users must see `IsFriend = true, IsPending = false`

2. **`SearchUsers_PendingRequest_ShouldShowCorrectPendingFlags`**
   - Verifies that pending state is symmetric for both sender and receiver
   
3. **`SearchUsers_AfterUnfriend_BothSidesShouldNotSeeFriendStatus`**
   - Verifies cleanup after unfriending

4. **`SearchUsers_ReverseRequest_ShouldNotifyWhenOtherSideSentRequest`**
   - Edge case: What happens if B tries to send request when A already sent one?

5. **`SearchUsers_MultipleUsers_ShouldMaintainSymmetryForAll`**
   - Tests with network of multiple users to ensure symmetry holds universally

### Manual Testing Steps
1. Create two test users (A and B)
2. Log in as User A
3. Send friend request to User B ? verify `IsPending = true` in search
4. Log in as User B  
5. Search for User A ? **VERIFY** `IsPending = true` (this was the bug)
6. Accept request
7. Search again from both sides ? **VERIFY** `IsFriend = true, IsPending = false`

### Database Verification Query
```sql
-- Check friendship state between two users
SELECT 
    SenderId,
    RecipientId,
    Status,
    RespondedAt,
    CreatedAtUtc
FROM friend_links
WHERE (SenderId = @UserA AND RecipientId = @UserB)
   OR (SenderId = @UserB AND RecipientId = @UserA);
```

Expected:
- **Before accept**: 1 row with `Status = 'Pending'`, `RespondedAt = NULL`
- **After accept**: same row with `Status = 'Accepted'`, `RespondedAt = <timestamp>`

## Future Improvements (Optional)

### Enhanced DTO
Consider adding more granular pending state information:

```csharp
public sealed record UserSearchItemDto(
    Guid UserId,
    string UserName,
    string FullName,
    string? AvatarUrl,
    string? University,
    bool IsFriend,
    bool IsPending,
    bool IsOutgoingPending,  // Current user sent request
    bool IsIncomingPending   // Current user received request
);
```

**Pros**: Frontend can show different UI for "You invited X" vs "X invited you"  
**Cons**: Breaking change for existing clients

### Alternative Fix (Not Recommended)
Keep the original logic but add both directions:

```csharp
var isOutgoingPending = link?.Status == FriendStatus.Pending && link.SenderId == currentUserId;
var isIncomingPending = link?.Status == FriendStatus.Pending && link.RecipientId == currentUserId;
var isPending = isOutgoingPending || isIncomingPending;
```

This is more verbose but makes the intent explicit. However, the simpler fix is preferred.

## Cache Considerations

If you're using Redis or any caching layer for friend search results:

1. **Cache Key Pattern**: `friends:search:{userId}:*`
2. **Invalidation Points**:
   - After `InviteAsync` ? invalidate both sender and recipient cache
   - After `AcceptAsync` ? invalidate both users' cache
   - After `DeclineAsync` / `CancelAsync` ? invalidate both users' cache

3. **Current Implementation**: The fix doesn't require cache changes if invalidation is already symmetric.

## Performance Impact

? **No performance degradation**

- The fix **removes** a condition check, making the query slightly faster
- Database query remains the same (already fetches bidirectional links)
- No additional database hits required

## Deployment Notes

1. **No migration needed** - this is purely a business logic fix
2. **No breaking changes** - DTO structure remains unchanged
3. **Backward compatible** - existing API clients continue to work
4. **Immediate effect** - no data migration or reprocessing required

## Summary

| Aspect | Before | After |
|--------|--------|-------|
| **Outgoing Pending** | ? Works | ? Works |
| **Incoming Pending** | ? Broken | ? Fixed |
| **Friend Status** | ? Works | ? Works |
| **Symmetry** | ? Asymmetric | ? Symmetric |

The fix ensures that friendship relationships are truly bidirectional and symmetric, matching user expectations and eliminating the confusing "one side is friends, the other isn't" bug.

---

**Date Fixed**: January 2025  
**Fixed By**: GitHub Copilot  
**Files Changed**: `Services/Friends/FriendService.cs` (1 line)  
**Tests Added**: `Tests/Services.Presence.Tests/Friendship_Symmetry_Tests.cs` (5 comprehensive tests)
