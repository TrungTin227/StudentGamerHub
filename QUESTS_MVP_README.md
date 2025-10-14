# Daily Quests System - MVP Implementation

## Overview
H? th?ng Daily Quests MVP cho Student Gamer Hub v?i 4 quest c? b?n:
- **CHECK_IN_DAILY** (+5 ?i?m): Check-in hôm nay
- **JOIN_ANY_ROOM** (+5 ?i?m): Tham gia b?t k? phòng
- **INVITE_ACCEPTED** (+10 ?i?m): L?i m?i b?n ???c ch?p nh?n
- **ATTEND_EVENT** (+20 ?i?m): ?i?m danh s? ki?n

## Key Features

### 1. **Timezone: Asia/Ho_Chi_Minh (UTC+7)**
- T?t c? quest reset vào **00:00 gi? VN** m?i ngày
- TTL Redis ???c tính ??n midnight VN ti?p theo

### 2. **Idempotent (SET NX + Rollback)**
- M?i quest ch? hoàn thành **1 l?n/ngày/user**
- Redis `SET NX` (when: NotExists) ??m b?o atomic idempotency
- Flag pattern: `q:{yyyyMMdd}:{userId}:{questCode}` = `"1"`
- TTL t? ??ng expire vào 00:00 VN hôm sau
- **Rollback**: N?u DB commit fail ? best-effort `DEL` flag ?? cho phép retry

### 3. **Transactional**
- C?ng ?i?m vào DB b?c trong `IGenericUnitOfWork.ExecuteTransactionAsync`
- Atomic flow:
  1. SET NX flag Redis (TTL)
  2. N?u FALSE ? return Validation error "Quest already completed today"
  3. N?u TRUE ? c?ng ?i?m vào DB (transaction)
  4. DB commit OK ? INCR analytics counter
  5. DB commit FAIL ? DEL flag (best-effort) + return Unexpected

### 4. **Analytics (Optional)**
- Counter key: `qc:done:{yyyyMMddHHmm}` (TTL 2h)
- Increment m?i l?n complete quest (best-effort)

## Architecture

```
DTOs/
??? Quests/
?   ??? QuestDtos.cs          // QuestTodayDto, QuestItemDto

Services/
??? Application/Quests/
?   ??? IQuestService.cs      // Interface
??? Quests/
    ??? QuestService.cs       // Implementation

WebAPI/
??? Controllers/
    ??? QuestsController.cs   // REST API endpoints
```

## API Endpoints

### GET /quests/today
L?y danh sách quest hôm nay + tr?ng thái hoàn thành + **Points hi?n t?i c?a user**.

**Response:**
```json
{
  "Points": 150,              // ? T?ng ?i?m hi?n t?i c?a user (t? DB)
  "Quests": [
    {
      "Code": "CHECK_IN_DAILY",
      "Title": "Check-in hôm nay",
      "Reward": 5,
      "Done": true            // ? Tr?ng thái hoàn thành hôm nay (t? Redis)
    },
    {
      "Code": "JOIN_ANY_ROOM",
      "Title": "Tham gia b?t k? phòng",
      "Reward": 5,
      "Done": true
    },
    {
      "Code": "INVITE_ACCEPTED",
      "Title": "L?i m?i b?n ???c ch?p nh?n",
      "Reward": 10,
      "Done": false
    },
    {
      "Code": "ATTEND_EVENT",
      "Title": "?i?m danh s? ki?n",
      "Reward": 20,
      "Done": false
    }
  ]
}
```

### POST /quests/check-in
Manual check-in quest (+5 ?i?m).
- **Success (first time)**: `204 No Content` + ?i?m t?ng
- **Idempotent (already done)**: `400 Bad Request` v?i message "Quest already completed today"

### POST /quests/join-room/{roomId}
Mark join room quest (+5 ?i?m).
- T??ng t? check-in, idempotent theo ngày
- Th??ng ???c g?i t? RoomService khi user join room thành công

### POST /quests/attend-event/{eventId}
Mark attend event quest (+20 ?i?m).
- T??ng t? check-in, idempotent theo ngày
- Th??ng ???c g?i t? EventService khi user check-in s? ki?n

## Integration Points

### 1. FriendService Hook
File: `Services/Friends/FriendService.cs`

```csharp
// Trong AcceptAsync() sau khi accept invite thành công:
if (_quests is not null)
{
    _ = await _quests.MarkInviteAcceptedAsync(
        link.SenderId,      // Ng??i g?i l?i m?i nh?n ?i?m
        link.RecipientId,
        innerCt
    ).ConfigureAwait(false);
}
```

### 2. RoomService Hook (TODO)
Khi user join room thành công:
```csharp
// Best-effort (không fail transaction n?u quest l?i)
if (_quests is not null)
{
    _ = await _quests.MarkJoinRoomAsync(userId, roomId, ct);
}
```

### 3. EventService Hook (TODO)
Khi user check-in s? ki?n:
```csharp
// Best-effort (không fail transaction n?u quest l?i)
if (_quests is not null)
{
    _ = await _quests.MarkAttendEventAsync(userId, eventId, ct);
}
```

## Redis Keys

### Quest Completion Flag
**Pattern:** `q:{yyyyMMdd}:{userId}:{questCode}`
- **Value:** `"1"`
- **TTL:** ??n 00:00 VN hôm sau
- **SET condition:** `When.NotExists` (idempotent)
- **Example:** `q:20250114:550e8400-e29b-41d4-a716-446655440000:CHECK_IN_DAILY`

### Analytics Counter (Optional)
**Pattern:** `qc:done:{yyyyMMddHHmm}`
- **Value:** Increment counter
- **TTL:** 2 hours
- **Example:** `qc:done:202501141530`

## Quest Codes & Rewards

| Code | Title | Reward | Trigger |
|------|-------|--------|---------|
| `CHECK_IN_DAILY` | Check-in hôm nay | 5 | Manual (POST /quests/check-in) |
| `JOIN_ANY_ROOM` | Tham gia b?t k? phòng | 5 | RoomService (khi join room) |
| `INVITE_ACCEPTED` | L?i m?i b?n ???c ch?p nh?n | 10 | FriendService (khi accept invite) |
| `ATTEND_EVENT` | ?i?m danh s? ki?n | 20 | EventService (khi check-in event) |

## Configuration

### appsettings.json
```json
{
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

Không c?n config riêng cho Quest. Titles/rewards hi?n t?i hardcode trong service (s? config hoá sau khi stable).

## Base Reuse Compliance

? **Result/Result<T>**: `BusinessObjects.Common.Results`
? **ExecuteTransactionAsync**: `Repositories.WorkSeeds.Extensions.TransactionExtensions`
? **Error.Codes**: Validation, NotFound, Conflict, Forbidden, Unexpected
? **No custom Result/Error/UoW**: Ch? dùng base có s?n

## HTTP Semantics

### Success Response
- **204 No Content**: Quest completed successfully (first time today)
- Body: Empty

### Idempotent Response
- **400 Bad Request**: Quest already completed today
- Body (ProblemDetails):
  ```json
  {
    "type": "https://httpstatuses.com/400",
    "title": "validation_error",
    "status": 400,
    "detail": "Quest already completed today",
    "code": "validation_error"
  }
  ```

### Error Responses
- **401 Unauthorized**: Missing or invalid authentication
- **404 Not Found**: User not found (khi c?ng ?i?m)
- **500 Internal Server Error**: Unexpected error

## Testing

### Manual Testing

#### 1. Check-in (first time)
```bash
curl -X POST http://localhost:5276/quests/check-in \
  -H "Authorization: Bearer {token}" \
  -v

# Expected: 204 No Content
# User.Points t?ng +5
# Redis key: q:20250114:{userId}:CHECK_IN_DAILY = "1" (TTL ??n 00:00 VN)
```

#### 2. Check-in (idempotent - second time same day)
```bash
curl -X POST http://localhost:5276/quests/check-in \
  -H "Authorization: Bearer {token}" \
  -v

# Expected: 400 Bad Request
# Body: {"detail": "Quest already completed today", ...}
# User.Points KHÔNG t?ng
```

#### 3. View today's quests
```bash
curl http://localhost:5276/quests/today \
  -H "Authorization: Bearer {token}"

# Expected: 200 OK
# Body: {"Points": 150, "Quests": [...]}
```

#### 4. Test timezone reset
```bash
# Tr??c 00:00 VN: check-in ? 204
# Sau 00:00 VN: check-in l?i ? 204 (quest reset)
# Redis key m?i: q:20250115:{userId}:CHECK_IN_DAILY
```

### Integration Testing
```csharp
// TODO: Unit tests cho QuestService
// - GetTodayAsync returns correct quest list + current Points
// - CompleteCheckInAsync is idempotent (SET NX)
// - TTL calculation correct for VN timezone
// - Points awarded in transaction
// - Flag rollback khi DB commit fail
```

### Redis Verification
```bash
# Check flag exists
redis-cli GET "q:20250114:{userId}:CHECK_IN_DAILY"
# Expected: "1"

# Check TTL (seconds to 00:00 VN)
redis-cli TTL "q:20250114:{userId}:CHECK_IN_DAILY"
# Expected: ~43200 (12 hours if checked at noon VN)

# Check analytics counter
redis-cli GET "qc:done:202501141530"
# Expected: "1" (or higher)
```

## Error Handling

### Idempotent Error (Expected)
```json
{
  "type": "https://httpstatuses.com/400",
  "title": "validation_error",
  "status": 400,
  "detail": "Quest already completed today",
  "code": "validation_error"
}
```

### User Not Found (DB)
```json
{
  "type": "https://httpstatuses.com/404",
  "title": "not_found",
  "status": 404,
  "detail": "User not found",
  "code": "not_found"
}
```

### Unexpected Error
```json
{
  "type": "https://httpstatuses.com/500",
  "title": "unexpected_error",
  "status": 500,
  "detail": "Quest completion failed: {exception message}",
  "code": "unexpected_error"
}
```

## Future Enhancements

### Phase 2
- [ ] Config hoá quest definitions (appsettings/DB)
- [ ] Quest progress tracking (ví d?: "Join 3 rooms")
- [ ] Daily/Weekly/Monthly quest types
- [ ] Quest chains (complete A to unlock B)
- [ ] Reward variations (items, badges, achievements)
- [ ] Rate limiting per quest action (Redis sliding window)

### Phase 3
- [ ] Quest dashboard (admin analytics)
- [ ] User quest history (DB log)
- [ ] Leaderboards (top questers)
- [ ] Notifications (quest completed, new quest available)
- [ ] Multi-timezone support

## Implementation Notes

### Idempotency Strategy
- **Redis SET NX**: Atomic check-and-set (không c?n SETNX + EXPIRE riêng)
- **Rollback on failure**: DEL flag n?u DB commit fail (best-effort, không ?nh h??ng transaction)
- **No race condition**: SET NX ??m b?o ch? 1 request thành công per day

### Transaction Safety
- **Atomic**: SET NX ? DB transaction ? INCR counter
- **Rollback**: DB fail ? DEL flag (best-effort)
- **No orphaned flags**: TTL t? ??ng cleanup vào 00:00 VN

### Best-Effort Operations
- **Analytics counter**: INCR không fail quest completion
- **Flag rollback**: DEL không fail n?u Redis down

### Redis Failure Handling
- **GET keys (GetTodayAsync)**: Return empty Done flags (graceful degradation)
- **SET NX (Complete)**: Return Unexpected error (cannot complete quest)
- **DEL (Rollback)**: Silent fail (flag s? expire t? ??ng)

## Deliverables Checklist

? DTOs: `QuestTodayDto`, `QuestItemDto`
? Interface: `IQuestService` v?i 5 methods (GetToday, CompleteCheckIn, MarkJoinRoom, MarkInviteAccepted, MarkAttendEvent)
? Implementation: `QuestService` v?i:
  - Helper TTL calculation (VN timezone)
  - Helper Redis key builder
  - Core idempotent completion (SET NX + rollback)
  - GetTodayAsync tr? Points hi?n t?i + Done flags
? Controller: `QuestsController` (GET today, POST check-in, POST join-room, POST attend-event)
? FriendService hook: Trigger InviteAccepted quest (best-effort)
? Build successful: No errors
? Convention DI: Auto-registered via `RegisterServiceInterfacesByConvention`
? HTTP semantics: 204 success, 400 idempotent, 404/500 errors
? Quest codes: `CHECK_IN_DAILY`, `JOIN_ANY_ROOM`, `INVITE_ACCEPTED`, `ATTEND_EVENT`
