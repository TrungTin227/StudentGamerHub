# Quest System - Acceptance Testing Guide

## Prerequisites
1. Redis running: `redis-server` (localhost:6379)
2. Database seeded with test user
3. Get JWT token from `/api/auth/login`

## Test Scenario 1: Check-in Idempotency (CRITICAL)

### Step 1: First check-in (SUCCESS)
```bash
curl -X POST http://localhost:5276/quests/check-in \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -v

# Expected Response:
# HTTP/1.1 204 No Content

# Verify Points increased:
curl http://localhost:5276/quests/today \
  -H "Authorization: Bearer YOUR_TOKEN"

# Expected: Points = (previous) + 5
```

### Step 2: Second check-in same day (IDEMPOTENT)
```bash
curl -X POST http://localhost:5276/quests/check-in \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -v

# Expected Response:
# HTTP/1.1 400 Bad Request
# Content-Type: application/problem+json
# {
#   "type": "https://httpstatuses.com/400",
#   "title": "validation_error",
#   "status": 400,
#   "detail": "Quest already completed today",
#   "code": "validation_error"
# }

# Verify Points NOT increased:
curl http://localhost:5276/quests/today \
  -H "Authorization: Bearer YOUR_TOKEN"

# Expected: Points unchanged
```

## Test Scenario 2: View Today's Quests

```bash
curl http://localhost:5276/quests/today \
  -H "Authorization: Bearer YOUR_TOKEN" | jq

# Expected Response:
{
  "Points": 150,
  "Quests": [
    {
      "Code": "CHECK_IN_DAILY",
      "Title": "Check-in hôm nay",
      "Reward": 5,
      "Done": true          # ? true n?u ?ã check-in hôm nay
    },
    {
      "Code": "JOIN_ANY_ROOM",
      "Title": "Tham gia b?t k? phòng",
      "Reward": 5,
      "Done": false
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

## Test Scenario 3: Redis Verification

```bash
# Get user ID from token (decode JWT at jwt.io)
USER_ID="550e8400-e29b-41d4-a716-446655440000"

# Today's date VN (UTC+7)
DATE_VN=$(TZ=Asia/Ho_Chi_Minh date +%Y%m%d)
echo "VN Date: $DATE_VN"

# Check Redis flag
redis-cli GET "q:${DATE_VN}:${USER_ID}:CHECK_IN_DAILY"
# Expected: "1" (if checked in today)

# Check TTL (seconds to midnight VN)
redis-cli TTL "q:${DATE_VN}:${USER_ID}:CHECK_IN_DAILY"
# Expected: positive number (seconds remaining to 00:00 VN)

# Check analytics counter
MINUTE=$(TZ=Asia/Ho_Chi_Minh date +%Y%m%d%H%M)
redis-cli GET "qc:done:${MINUTE}"
# Expected: number of quest completions this minute
```

## Test Scenario 4: Timezone Reset (Manual)

### Before 00:00 VN
```bash
# Check-in ? 204 OK
curl -X POST http://localhost:5276/quests/check-in \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -v

# Points t?ng +5
```

### After 00:00 VN (next day)
```bash
# Check-in l?i ? 204 OK (quest reset!)
curl -X POST http://localhost:5276/quests/check-in \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -v

# Points t?ng thêm +5
# Redis key m?i: q:20250115:{userId}:CHECK_IN_DAILY
```

## Test Scenario 5: Friend Invite Accepted (Integration)

### User A invites User B
```bash
curl -X POST http://localhost:5276/friends/{USER_B_ID}/invite \
  -H "Authorization: Bearer USER_A_TOKEN" \
  -v

# Expected: 204 No Content
```

### User B accepts invite
```bash
curl -X POST http://localhost:5276/friends/{USER_A_ID}/accept \
  -H "Authorization: Bearer USER_B_TOKEN" \
  -v

# Expected: 204 No Content

# ? AUTOMATIC: User A's quest "INVITE_ACCEPTED" completed
# User A's Points should increase +10
```

### Verify User A's quest completed
```bash
curl http://localhost:5276/quests/today \
  -H "Authorization: Bearer USER_A_TOKEN" | jq '.Quests[] | select(.Code == "INVITE_ACCEPTED")'

# Expected:
{
  "Code": "INVITE_ACCEPTED",
  "Title": "L?i m?i b?n ???c ch?p nh?n",
  "Reward": 10,
  "Done": true          # ? true!
}
```

## Test Scenario 6: Database Transaction Rollback (Advanced)

### Simulate DB failure (requires code modification for testing)
```csharp
// In QuestService.CompleteQuestAsync, before SaveChangesAsync:
throw new Exception("Simulated DB failure");
```

### Expected behavior:
1. SET NX flag Redis ? TRUE
2. DB transaction ? FAIL (exception)
3. DEL flag Redis (rollback) ? best-effort
4. Return Unexpected error

### Verify:
```bash
# Check Redis flag NOT exists
redis-cli GET "q:${DATE_VN}:${USER_ID}:CHECK_IN_DAILY"
# Expected: (nil)

# Points NOT increased
```

## Acceptance Criteria ?

### ? Criterion 1: Idempotency
- [ ] First POST /quests/check-in ? 204 No Content
- [ ] Second POST same day ? 400 Bad Request "Quest already completed today"
- [ ] Points only increase once

### ? Criterion 2: Redis Flag
- [ ] Key pattern: `q:{yyyyMMdd}:{userId}:{questCode}`
- [ ] Value: `"1"`
- [ ] TTL: expires at 00:00 VN next day

### ? Criterion 3: Analytics Counter
- [ ] Key pattern: `qc:done:{yyyyMMddHHmm}`
- [ ] INCR on success
- [ ] TTL: 2 hours

### ? Criterion 4: Transaction Safety
- [ ] Points updated in DB within transaction
- [ ] Flag rollback (DEL) if DB commit fails

### ? Criterion 5: HTTP Semantics
- [ ] Success: 204 No Content (empty body)
- [ ] Idempotent: 400 Bad Request (ProblemDetails)
- [ ] User not found: 404 Not Found
- [ ] Unexpected: 500 Internal Server Error

### ? Criterion 6: Integration
- [ ] FriendService.Accept triggers INVITE_ACCEPTED quest
- [ ] User A (inviter) receives +10 points
- [ ] Idempotent: only once per day

## Quick Verification Commands

```bash
# Set variables
TOKEN="YOUR_JWT_TOKEN"
USER_ID="YOUR_USER_ID"
DATE_VN=$(TZ=Asia/Ho_Chi_Minh date +%Y%m%d)

# 1. Check-in
curl -X POST http://localhost:5276/quests/check-in \
  -H "Authorization: Bearer $TOKEN" \
  -w "\nHTTP Status: %{http_code}\n"

# 2. View quests
curl http://localhost:5276/quests/today \
  -H "Authorization: Bearer $TOKEN" | jq

# 3. Verify Redis
redis-cli GET "q:${DATE_VN}:${USER_ID}:CHECK_IN_DAILY"
redis-cli TTL "q:${DATE_VN}:${USER_ID}:CHECK_IN_DAILY"

# 4. Check-in again (idempotent)
curl -X POST http://localhost:5276/quests/check-in \
  -H "Authorization: Bearer $TOKEN" \
  -w "\nHTTP Status: %{http_code}\n"
```

## Known Issues / Limitations

1. **Redis down**: Quest system unavailable (no fallback)
2. **Timezone hardcoded**: VN timezone (UTC+7) only
3. **Best-effort rollback**: DEL flag may fail silently if Redis down
4. **No rate limiting**: Can spam check-in endpoint (returns 400 but still makes requests)

## Next Steps

1. **RoomService integration**: Hook `MarkJoinRoomAsync` khi user join room
2. **EventService integration**: Hook `MarkAttendEventAsync` khi user check-in event
3. **Rate limiting**: Add per-user rate limit (10/min per quest action)
4. **Monitoring**: Log quest completion metrics
5. **Tests**: Unit tests cho QuestService (idempotency, TTL, rollback)
