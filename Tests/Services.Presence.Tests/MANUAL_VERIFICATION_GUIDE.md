# Manual Verification Script

## Quick Test to Verify the Friendship Symmetry Fix

### Using Postman / HTTP Client

#### 1. Setup Test Users

```http
### Register User A
POST {{baseUrl}}/api/Auth/register
Content-Type: application/json

{
  "userName": "testuser_a",
  "email": "a@test.com",
  "password": "Test@123456",
  "fullName": "Test User A"
}

### Register User B
POST {{baseUrl}}/api/Auth/register
Content-Type: application/json

{
  "userName": "testuser_b",
  "email": "b@test.com",
  "password": "Test@123456",
  "fullName": "Test User B"
}
```

#### 2. Login and Get Tokens

```http
### Login as User A
POST {{baseUrl}}/api/Auth/login
Content-Type: application/json

{
  "email": "a@test.com",
  "password": "Test@123456"
}

# Copy the JWT token from response: {{tokenA}}

### Login as User B
POST {{baseUrl}}/api/Auth/login
Content-Type: application/json

{
  "email": "b@test.com",
  "password": "Test@123456"
}

# Copy the JWT token from response: {{tokenB}}
```

#### 3. Test the Bug Fix

```http
### Step 1: User A sends friend request to User B
POST {{baseUrl}}/api/Friends/{{userBId}}/invite
Authorization: Bearer {{tokenA}}

# Expected: 204 No Content

### Step 2: User A searches for User B (should see IsPending = true)
GET {{baseUrl}}/api/Friends/search?q=testuser_b
Authorization: Bearer {{tokenA}}

# Expected Response:
# {
#   "items": [
#     {
#       "userId": "...",
#       "userName": "testuser_b",
#       "fullName": "Test User B",
#       "isFriend": false,
#       "isPending": true  ? Was working before
#     }
#   ]
# }

### Step 3: User B searches for User A (CRITICAL - this was the bug!)
GET {{baseUrl}}/api/Friends/search?q=testuser_a
Authorization: Bearer {{tokenB}}

# Expected Response (AFTER FIX):
# {
#   "items": [
#     {
#       "userId": "...",
#       "userName": "testuser_a",
#       "fullName": "Test User A",
#       "isFriend": false,
#       "isPending": true  ? NOW FIXED! Was false before
#     }
#   ]
# }

### Step 4: User B accepts the friend request
POST {{baseUrl}}/api/Friends/{{userAId}}/accept
Authorization: Bearer {{tokenB}}

# Expected: 204 No Content

### Step 5: Verify both sides see each other as friends
GET {{baseUrl}}/api/Friends/search?q=testuser_b
Authorization: Bearer {{tokenA}}

# Expected:
# {
#   "items": [
#     {
#       "userId": "...",
#       "userName": "testuser_b",
#       "fullName": "Test User B",
#       "isFriend": true,   ?
#       "isPending": false  ?
#     }
#   ]
# }

GET {{baseUrl}}/api/Friends/search?q=testuser_a
Authorization: Bearer {{tokenB}}

# Expected:
# {
#   "items": [
#     {
#       "userId": "...",
#       "userName": "testuser_a",
#       "fullName": "Test User A",
#       "isFriend": true,   ?
#       "isPending": false  ?
#     }
#   ]
# }
```

### Using cURL

```bash
#!/bin/bash

BASE_URL="http://localhost:5000"

# Register User A
curl -X POST "$BASE_URL/api/Auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "testuser_a",
    "email": "a@test.com",
    "password": "Test@123456",
    "fullName": "Test User A"
  }'

# Register User B
curl -X POST "$BASE_URL/api/Auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "testuser_b",
    "email": "b@test.com",
    "password": "Test@123456",
    "fullName": "Test User B"
  }'

# Login as A and get token
TOKEN_A=$(curl -X POST "$BASE_URL/api/Auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "a@test.com",
    "password": "Test@123456"
  }' | jq -r '.token')

# Login as B and get token  
TOKEN_B=$(curl -X POST "$BASE_URL/api/Auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "b@test.com",
    "password": "Test@123456"
  }' | jq -r '.token')

# Get User B's ID
USER_B_ID=$(curl -X GET "$BASE_URL/api/Friends/search?q=testuser_b" \
  -H "Authorization: Bearer $TOKEN_A" | jq -r '.items[0].userId')

# A sends friend request to B
curl -X POST "$BASE_URL/api/Friends/$USER_B_ID/invite" \
  -H "Authorization: Bearer $TOKEN_A"

echo "=== User A searches for User B (should see isPending: true) ==="
curl -X GET "$BASE_URL/api/Friends/search?q=testuser_b" \
  -H "Authorization: Bearer $TOKEN_A" | jq '.items[0] | {userName, isFriend, isPending}'

echo "=== User B searches for User A (BUG CHECK: should see isPending: true) ==="
curl -X GET "$BASE_URL/api/Friends/search?q=testuser_a" \
  -H "Authorization: Bearer $TOKEN_B" | jq '.items[0] | {userName, isFriend, isPending}'

# Get User A's ID
USER_A_ID=$(curl -X GET "$BASE_URL/api/Friends/search?q=testuser_a" \
  -H "Authorization: Bearer $TOKEN_B" | jq -r '.items[0].userId')

# B accepts request
curl -X POST "$BASE_URL/api/Friends/$USER_A_ID/accept" \
  -H "Authorization: Bearer $TOKEN_B"

echo "=== After Accept: User A searches for User B ==="
curl -X GET "$BASE_URL/api/Friends/search?q=testuser_b" \
  -H "Authorization: Bearer $TOKEN_A" | jq '.items[0] | {userName, isFriend, isPending}'

echo "=== After Accept: User B searches for User A ==="
curl -X GET "$BASE_URL/api/Friends/search?q=testuser_a" \
  -H "Authorization: Bearer $TOKEN_B" | jq '.items[0] | {userName, isFriend, isPending}'
```

### Using SQL to Verify Database State

```sql
-- View the friendship link
SELECT 
    fl.Id,
    u1.UserName as Sender,
    u2.UserName as Recipient,
    fl.Status,
    fl.CreatedAtUtc,
    fl.RespondedAt,
    fl.PairMinUserId,
    fl.PairMaxUserId
FROM friend_links fl
JOIN users u1 ON fl.SenderId = u1.Id
JOIN users u2 ON fl.RecipientId = u2.Id
WHERE u1.Email IN ('a@test.com', 'b@test.com')
   OR u2.Email IN ('a@test.com', 'b@test.com');

-- Expected output after invite:
-- | Sender      | Recipient   | Status  | RespondedAt |
-- |-------------|-------------|---------|-------------|
-- | testuser_a  | testuser_b  | Pending | NULL        |

-- Expected output after accept:
-- | Sender      | Recipient   | Status   | RespondedAt        |
-- |-------------|-------------| ---------|-------------------|
-- | testuser_a  | testuser_b  | Accepted | 2025-01-19 10:30:00|
```

## Expected Results Summary

### Before the Fix
| Scenario | User A View | User B View | Issue |
|----------|-------------|-------------|-------|
| After invite | `isPending: true` ? | `isPending: false` ? | Asymmetric! |
| After accept | `isFriend: true` ? | `isFriend: true` ? | Works |

### After the Fix
| Scenario | User A View | User B View | Status |
|----------|-------------|-------------|--------|
| After invite | `isPending: true` ? | `isPending: true` ? | **FIXED!** Symmetric! |
| After accept | `isFriend: true` ? | `isFriend: true` ? | Works |

## Troubleshooting

### If tests still fail:

1. **Clear cache** (if using Redis):
   ```bash
   redis-cli FLUSHDB
   ```

2. **Restart the application**:
   ```bash
   dotnet run --project WebAPI
   ```

3. **Check logs** for any errors during friend operations

4. **Verify database state** using SQL queries above

5. **Check JWT tokens** are valid and not expired

## Success Criteria

? Both users see `isPending: true` when request is pending  
? Both users see `isFriend: true, isPending: false` after acceptance  
? No errors in console/logs  
? Database shows exactly one `FriendLink` record between the two users  
? Refresh the page - state persists correctly  

---

**Note**: This verification should be done after deploying the fix to ensure the bug is completely resolved in the production environment.
