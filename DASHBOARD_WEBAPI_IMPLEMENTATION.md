# Dashboard/Today Feature - WebAPI Layer Implementation

## Summary

Successfully implemented the **WebAPI layer** for the Dashboard/Today feature with controller, rate limiting, and Scalar/OpenAPI documentation.

## Files Created/Modified

### 1. **WebAPI/Controllers/DashboardController.cs** (NEW)
```csharp
[ApiController]
[Route("dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    [HttpGet("today")]
    [EnableRateLimiting("DashboardRead")]
    [ProducesResponseType(typeof(DashboardTodayDto), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetToday(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
            return this.ToActionResult(Result<DashboardTodayDto>.Failure(...));

        var result = await _dashboard.GetTodayAsync(userId.Value, ct);
        return this.ToActionResult(result, v => v);
    }
}
```

### 2. **WebAPI/Extensions/ServiceCollectionExtensions.cs** (MODIFIED)
Added DashboardRead rate limiting policy:
```csharp
options.AddPolicy("DashboardRead", httpContext =>
{
    var userKey = httpContext.User.GetUserId()?.ToString() ?? Guid.Empty.ToString();

    return RateLimitPartition.GetTokenBucketLimiter(userKey, _ => new TokenBucketRateLimiterOptions
    {
        TokenLimit = 120,
        TokensPerPeriod = 120,
        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0,
        AutoReplenishment = true
    });
});
```

## Implementation Details

### Architecture Compliance ?

| Requirement | Status | Details |
|------------|--------|---------|
| Controller in WebAPI | ? | `DashboardController` created |
| Uses ResultHttpExtensions | ? | `this.ToActionResult(result, v => v)` |
| Uses ClaimsPrincipalExtensions | ? | `User.GetUserId()` |
| [Authorize] attribute | ? | Required JWT token |
| Rate limiting | ? | 120 requests/minute per user |
| NO domain logic | ? | Only orchestration + mapping |
| NO new Result/Error | ? | Reuses existing types |
| UseRateLimiter in pipeline | ? | Already configured in OperationalPipeline |

### Endpoint Details

#### **GET /dashboard/today**

**Authentication**: Required (`[Authorize]`)  
**Rate Limit**: 120 requests per minute per user  
**Method**: GET  
**Route**: `/dashboard/today`  

**Response 200 - Success**:
```json
{
  "points": 125,
  "quests": {
    "points": 15,
    "quests": [
      {
        "code": "CHECK_IN_DAILY",
        "title": "Check-in Daily",
        "reward": 5,
        "done": true
      },
      {
        "code": "JOIN_ANY_ROOM",
        "title": "Join Any Room",
        "reward": 5,
        "done": false
      },
      {
        "code": "INVITE_ACCEPTED",
        "title": "Invite Accepted",
        "reward": 10,
        "done": false
      },
      {
        "code": "ATTEND_EVENT",
        "title": "Attend Event",
        "reward": 20,
        "done": false
      }
    ]
  },
  "eventsToday": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "title": "Mini Tournament",
      "startsAt": "2025-01-15T14:00:00+07:00",
      "endsAt": "2025-01-15T18:00:00+07:00",
      "location": "Online",
      "mode": "Online"
    }
  ],
  "activity": {
    "onlineFriends": 8,
    "questsDoneLast60m": 34
  }
}
```

**Response 401 - Unauthorized**:
```json
{
  "type": "https://httpstatuses.com/401",
  "title": "Unauthorized",
  "status": 401,
  "detail": "User identity is required.",
  "code": "Unauthorized",
  "traceId": "00-xxxxx..."
}
```

**Response 429 - Too Many Requests**:
```json
{
  "type": "https://httpstatuses.com/429",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Maximum 120 requests per minute."
}
```

**Response 500 - Internal Server Error**:
```json
{
  "type": "https://httpstatuses.com/500",
  "title": "Unexpected",
  "status": 500,
  "detail": "Failed to get dashboard data: <error message>",
  "code": "Unexpected",
  "traceId": "00-xxxxx..."
}
```

### Rate Limiting Configuration

**Policy Name**: `DashboardRead`  
**Algorithm**: Token Bucket  
**Limit**: 120 tokens per minute per user  
**Replenishment**: 120 tokens every 1 minute  
**Queue**: Disabled (QueueLimit = 0)  
**Partition Key**: User ID from JWT claims  
**Status Code**: 429 (Too Many Requests)  

**Why Token Bucket?**
- Allows bursts of requests up to 120
- Tokens replenish continuously
- Fair per-user limits
- Prevents abuse while allowing normal usage

### Controller Implementation

```csharp
[ApiController]
[Route("dashboard")]
[Produces("application/json")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard)
    {
        _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
    }

    [HttpGet("today")]
    [EnableRateLimiting("DashboardRead")]
    [ProducesResponseType(typeof(DashboardTodayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetToday(CancellationToken ct)
    {
        // 1. Extract user ID from JWT claims
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return this.ToActionResult(Result<DashboardTodayDto>.Failure(
                new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        // 2. Call service layer
        var result = await _dashboard.GetTodayAsync(userId.Value, ct);
        
        // 3. Map Result to HTTP response
        return this.ToActionResult(result, v => v);
    }
}
```

### Key Features

#### 1. **Authorization**
- `[Authorize]` attribute requires valid JWT token
- Uses `User.GetUserId()` from ClaimsPrincipalExtensions
- Returns 401 if user not authenticated

#### 2. **Rate Limiting**
- `[EnableRateLimiting("DashboardRead")]` applies policy
- 120 requests per minute per user
- Prevents abuse and protects backend resources
- Returns 429 if limit exceeded

#### 3. **Result Mapping**
- Uses `ResultHttpExtensions.ToActionResult`
- Maps `Result<T>.Success` ? 200 OK with payload
- Maps `Result<T>.Failure` ? ProblemDetails with appropriate status code
- No manual HTTP status code handling

#### 4. **OpenAPI/Scalar Documentation**
- `[ProducesResponseType]` attributes document responses
- Scalar UI at `/docs` shows:
  - Endpoint details
  - Request/response schemas
  - Example payloads
  - HTTP status codes

### Request Flow

```
???????????????????????????????????????????????????????
?          Client sends GET /dashboard/today          ?
?              with Bearer token in header            ?
???????????????????????????????????????????????????????
                         ?
                         ?
           ????????????????????????????
           ?  Middleware Pipeline     ?
           ?  - Authentication        ?
           ?  - Authorization         ?
           ?  - Rate Limiting         ?
           ????????????????????????????
                         ?
                         ?
           ????????????????????????????
           ?  DashboardController     ?
           ?  - Extract userId        ?
           ?  - Validate identity     ?
           ????????????????????????????
                         ?
                         ?
           ????????????????????????????
           ?   DashboardService       ?
           ?   - Fetch all data       ?
           ?   - Aggregate results    ?
           ????????????????????????????
                         ?
                         ?
           ????????????????????????????
           ?   Result<DashboardDto>   ?
           ?   - Success: 200 + data  ?
           ?   - Failure: ProblemDetails ?
           ????????????????????????????
                         ?
                         ?
           ????????????????????????????
           ?   ToActionResult         ?
           ?   - Map to HTTP response ?
           ????????????????????????????
```

### Scalar/OpenAPI Documentation

**Access**: `http://localhost:5000/docs`

**Features**:
- ? Interactive API explorer
- ? Request/response schemas
- ? Example payloads
- ? Try-it-out functionality
- ? Authentication support (Bearer token)
- ? HTTP status code documentation

**DTOs Displayed**:
- `DashboardTodayDto`
- `QuestTodayDto`
- `QuestItemDto`
- `EventBriefDto`
- `ActivityDto`

### Testing Recommendations

#### **Manual Testing with Scalar**

1. Navigate to `/docs`
2. Authorize with JWT token
3. Execute GET /dashboard/today
4. Verify response structure

#### **Manual Testing with curl**

```bash
# Get JWT token first (login)
TOKEN="your-jwt-token"

# Call dashboard endpoint
curl -X GET "http://localhost:5000/dashboard/today" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Accept: application/json"
```

#### **Rate Limit Testing**

```bash
# Send 121 requests in 1 minute
for i in {1..121}; do
  curl -X GET "http://localhost:5000/dashboard/today" \
    -H "Authorization: Bearer $TOKEN" \
    -w "\n%{http_code}\n"
done

# Expected: First 120 return 200, 121st returns 429
```

#### **Unit Tests** (Recommended)

```csharp
[Fact]
public async Task GetToday_WithValidUser_ShouldReturn200()
{
    // Arrange: Mock service, setup user claims
    // Act: Call GetToday
    // Assert: Verify 200 OK with DashboardTodayDto
}

[Fact]
public async Task GetToday_WithoutAuth_ShouldReturn401()
{
    // Arrange: No user claims
    // Act: Call GetToday
    // Assert: Verify 401 Unauthorized
}

[Fact]
public async Task GetToday_WithServiceError_ShouldReturn500()
{
    // Arrange: Service returns error
    // Act: Call GetToday
    // Assert: Verify 500 with ProblemDetails
}
```

#### **Integration Tests** (Recommended)

```csharp
[Fact]
public async Task GetToday_E2E_ShouldReturnCompleteData()
{
    // Setup: Real data in DB + Redis
    // Act: HTTP GET /dashboard/today
    // Assert: Verify all fields populated
}

[Fact]
public async Task GetToday_TimezoneTest_ShouldReturnTodayEventsOnly()
{
    // Setup: Events at 23:30 yesterday, 09:00 today, 00:10 tomorrow (VN time)
    // Act: GET /dashboard/today
    // Assert: Only 09:00 event returned
}

[Fact]
public async Task GetToday_RateLimit_ShouldEnforce120PerMinute()
{
    // Act: Send 121 requests
    // Assert: First 120 succeed, 121st gets 429
}
```

### Error Handling

All errors from the service layer are automatically mapped to appropriate HTTP responses:

| Error Code | HTTP Status | Example |
|------------|-------------|---------|
| `Validation` | 400 Bad Request | Invalid user ID |
| `Unauthorized` | 401 Unauthorized | Missing or invalid token |
| `Forbidden` | 403 Forbidden | Insufficient permissions |
| `NotFound` | 404 Not Found | User not found |
| `Conflict` | 409 Conflict | Data conflict |
| `Unexpected` | 500 Internal Server Error | Redis connection error |

**Error Response Structure** (ProblemDetails RFC 7807):
```json
{
  "type": "https://httpstatuses.com/500",
  "title": "Unexpected",
  "status": 500,
  "detail": "Failed to get dashboard data: Redis connection timeout",
  "code": "Unexpected",
  "traceId": "00-abc123..."
}
```

### Pipeline Configuration

**Rate Limiting Pipeline**:
```
UseOperationalPipeline (Program.cs)
  ??> UseForwardedHeaders
  ??> UseHsts (prod)
  ??> UseResponseCompression
  ??> UseResponseCaching
  ??> UseRateLimiter ? (Applied here)
  ??> UseRequestTimeouts
```

**WebAPI Pipeline**:
```
UseWebApi (Program.cs)
  ??> UseExceptionHandler (AppExceptionHandler)
  ??> MapOpenApi (/openapi/v1.json)
  ??> MapScalarApiReference (/docs)
  ??> UseHttpsRedirection
  ??> UseCors
  ??> UseAuthentication ?
  ??> UseAuthorization ?
  ??> MapControllers
```

## Build Status

? **All files compile successfully**  
? **No build errors**  
? **No layer violations**  
? **No new Result/Error types**  
? **Uses ResultHttpExtensions**  
? **Uses ClaimsPrincipalExtensions**  
? **Rate limiting configured**  
? **OpenAPI documentation generated**

## Verification Commands

```bash
# Verify controller uses extensions
Select-String -Path "WebAPI\Controllers\DashboardController.cs" -Pattern "ToActionResult|GetUserId"

# Verify no new Result/Error types
Select-String -Path "WebAPI\Controllers\DashboardController.cs" -Pattern "class.*Result|class.*Error"

# Verify rate limiting policy
Select-String -Path "WebAPI\Extensions\ServiceCollectionExtensions.cs" -Pattern "DashboardRead"

# Verify UseRateLimiter in pipeline
Select-String -Path "WebAPI\Extensions\OperationalAppExtensions.cs" -Pattern "UseRateLimiter"

# Build test
dotnet build WebAPI/WebAPI.csproj
```

## Complete Feature Summary

### All Layers Implemented ?

1. **DTOs** (3 files)
   - DashboardTodayDto
   - EventBriefDto
   - ActivityDto

2. **Repositories** (4 files)
   - IEventQueries + EventQueries
   - IFriendLinkQueries + FriendLinkQueries

3. **Services** (2 files)
   - IDashboardService + DashboardService

4. **WebAPI** (2 files)
   - DashboardController
   - ServiceCollectionExtensions (rate limiting)

### Performance Characteristics ?

- **Parallel Execution**: All data sources fetched concurrently
- **Batch Operations**: 1 Redis pipeline for N friends, 1 MGET for 60 keys
- **Minimal Queries**: Project only needed fields, AsNoTracking
- **Index Usage**: StartsAt for events, Id for users
- **Rate Limiting**: 120 req/min prevents abuse

### API Endpoints

| Method | Endpoint | Auth | Rate Limit | Description |
|--------|----------|------|------------|-------------|
| GET | /dashboard/today | ? | 120/min | Get today's dashboard data |

---

**Implementation Date**: 2025  
**Phase**: Dashboard/Today - WebAPI Layer  
**Status**: ? Complete and verified  
**Documentation**: ? Scalar at /docs
