# Club Service Layer - Usage Guide

## ?? Overview

The Club Service Layer provides business logic for managing clubs within communities. It follows the Result pattern for error handling and uses the Unit of Work pattern for transaction management.

## ??? Architecture

### Layer Structure
```
Services/
??? Interfaces/
?   ??? IClubService.cs          # Service contract
??? Implementations/
?   ??? ClubService.cs            # Service implementation
??? Common/
    ??? Mapping/
        ??? ClubMappers.cs        # Club ? DTO mapping

Repositories/
??? Interfaces/
?   ??? IClubQueryRepository.cs   # Read operations
?   ??? IClubCommandRepository.cs # Write operations
??? Implements/
    ??? ClubQueryRepository.cs    # Query implementation
    ??? ClubCommandRepository.cs  # Command implementation

DTOs/
??? Clubs/
    ??? ClubBriefDto.cs           # Brief club information
    ??? ClubCreateRequestDto.cs   # Create request DTO
```

### Dependencies
- **IGenericUnitOfWork**: Transaction management
- **IClubQueryRepository**: Read operations
- **IClubCommandRepository**: Write operations

### Automatic Registration
The service is automatically registered via convention-based DI scanning:
```csharp
// No manual registration needed!
// Services.Common.DependencyInjection scans for classes ending with "Service"
services.AddApplicationServices(configuration);
```

## ?? API Methods

### 1. SearchAsync - Search Clubs

Search clubs within a community with filtering and cursor-based pagination.

#### Signature
```csharp
Task<Result<CursorPageResult<ClubBriefDto>>> SearchAsync(
    Guid communityId,
    string? name,
    bool? isPublic,
    int? membersFrom,
    int? membersTo,
    CursorRequest cursor,
    CancellationToken ct = default);
```

#### Parameters
- **communityId**: Community ID to search within (required)
- **name**: Filter by club name (case-insensitive partial match, optional)
- **isPublic**: Filter by visibility (true = public, false = private, null = all)
- **membersFrom**: Minimum members count (inclusive, >= 0)
- **membersTo**: Maximum members count (inclusive, >= 0)
- **cursor**: Cursor pagination request (size, direction, etc.)
- **ct**: Cancellation token

#### Sorting
- Primary: `MembersCount DESC` (most popular clubs first)
- Secondary: `Id DESC` (stable sort for pagination)

#### Validation Rules
- `membersFrom` must be >= 0
- `membersTo` must be >= 0
- `membersFrom` must be <= `membersTo`

#### Example Usage
```csharp
// Inject service
private readonly IClubService _clubService;

// Search public clubs with 10-50 members
var cursor = new CursorRequest(Cursor: null, Size: 20);
var result = await _clubService.SearchAsync(
    communityId: communityId,
    name: "gaming",
    isPublic: true,
    membersFrom: 10,
    membersTo: 50,
    cursor: cursor,
    ct: cancellationToken
);

if (result.IsSuccess)
{
    var clubs = result.Value.Items;
    var nextCursor = result.Value.NextCursor;
    
    // Display clubs...
    
    // Load next page if available
    if (!string.IsNullOrEmpty(nextCursor))
    {
        var nextRequest = cursor with { Cursor = nextCursor };
        var nextResult = await _clubService.SearchAsync(
            communityId, name, isPublic, membersFrom, membersTo,
            nextRequest, cancellationToken);
    }
}
else
{
    // Handle error
    var error = result.Error;
    Console.WriteLine($"Error: {error.Message}");
}
```

#### Response Structure
```csharp
CursorPageResult<ClubBriefDto>
{
    Items: [
        {
            Id: Guid,
            CommunityId: Guid,
            Name: string,
            IsPublic: bool,
            MembersCount: int,
            Description: string?
        },
        ...
    ],
    NextCursor: string?,  // Token for next page
    PrevCursor: null,     // Not implemented in v1
    Size: int,            // Page size
    Sort: "Id",           // Sort key
    Desc: true            // Sort direction
}
```

#### Error Cases
| Error Code | Condition | Message |
|-----------|-----------|---------|
| `Validation` | `membersFrom < 0` | "membersFrom must be >= 0." |
| `Validation` | `membersTo < 0` | "membersTo must be >= 0." |
| `Validation` | `membersFrom > membersTo` | "membersFrom must be <= membersTo." |

---

### 2. CreateClubAsync - Create Club

Create a new club within a community.

#### Signature
```csharp
Task<Result<Guid>> CreateClubAsync(
    Guid currentUserId,
    Guid communityId,
    string name,
    string? description,
    bool isPublic,
    CancellationToken ct = default);
```

#### Parameters
- **currentUserId**: Current user ID (for audit trail)
- **communityId**: Community ID where the club will be created
- **name**: Club name (required, 1-256 characters, will be trimmed)
- **description**: Optional club description (will be trimmed)
- **isPublic**: Public/private flag
- **ct**: Cancellation token

#### Behavior
- Initial `MembersCount` is set to 0
- Name and description are automatically trimmed
- Audit fields (`CreatedBy`, `CreatedAtUtc`) are auto-populated by DbContext
- Transaction-wrapped (automatic rollback on failure)
- **Important**: Creator is NOT auto-added as member (membership is room-level only)

#### Validation Rules
- Name is required (not null/empty/whitespace)
- Name must not exceed 256 characters
- Name is trimmed before validation

#### Example Usage
```csharp
// Create a public gaming club
var result = await _clubService.CreateClubAsync(
    currentUserId: userId,
    communityId: communityId,
    name: "Competitive Gaming Club",
    description: "For serious gamers who love competition",
    isPublic: true,
    ct: cancellationToken
);

if (result.IsSuccess)
{
    var clubId = result.Value;
    Console.WriteLine($"Club created with ID: {clubId}");
    
    // Next: Create rooms within this club
    // or redirect user to club page
}
else
{
    // Handle error
    switch (result.Error.Code)
    {
        case Error.Codes.Validation:
            // Display validation message to user
            break;
        case Error.Codes.Unexpected:
            // Log error and show generic message
            break;
    }
}
```

#### Response
- **Success**: `Result<Guid>` with new club ID
- **Failure**: `Result<Guid>` with error details

#### Error Cases
| Error Code | Condition | Message |
|-----------|-----------|---------|
| `Validation` | Name is null/empty/whitespace | "Club name is required." |
| `Validation` | Name exceeds 256 characters | "Club name must not exceed 256 characters." |
| `Unexpected` | Database/transaction failure | (varies) |

#### Database Effects
```sql
INSERT INTO clubs (
    Id, CommunityId, Name, Description, IsPublic, MembersCount,
    CreatedAtUtc, CreatedBy, IsDeleted
) VALUES (
    @Id, @CommunityId, @Name, @Description, @IsPublic, 0,
    NOW(), @CurrentUserId, false
);
```

---

### 3. GetByIdAsync - Get Club by ID

Retrieve a single club by its ID.

#### Signature
```csharp
Task<Result<ClubBriefDto>> GetByIdAsync(
    Guid clubId,
    CancellationToken ct = default);
```

#### Parameters
- **clubId**: Club ID (required)
- **ct**: Cancellation token

#### Example Usage
```csharp
// Get club details
var result = await _clubService.GetByIdAsync(clubId, cancellationToken);

if (result.IsSuccess)
{
    var club = result.Value;
    Console.WriteLine($"Club: {club.Name}");
    Console.WriteLine($"Members: {club.MembersCount}");
    Console.WriteLine($"Visibility: {(club.IsPublic ? "Public" : "Private")}");
}
else if (result.Error.Code == Error.Codes.NotFound)
{
    // Club not found - show 404 page
    Console.WriteLine("Club does not exist");
}
```

#### Response
- **Success**: `Result<ClubBriefDto>` with club details
- **Failure**: `Result<ClubBriefDto>` with NotFound error

#### Error Cases
| Error Code | Condition | Message |
|-----------|-----------|---------|
| `NotFound` | Club doesn't exist | "Club with ID '{clubId}' not found." |

#### Response Structure
```csharp
ClubBriefDto
{
    Id: Guid,
    CommunityId: Guid,
    Name: string,
    IsPublic: bool,
    MembersCount: int,
    Description: string?
}
```

---

## ?? Result Pattern

All methods return `Result<T>` or `Result` for consistent error handling.

### Success Flow
```csharp
var result = await _clubService.GetByIdAsync(clubId);

if (result.IsSuccess)
{
    var club = result.Value;
    // Use club data...
}
```

### Error Handling
```csharp
var result = await _clubService.CreateClubAsync(...);

if (result.IsFailure)
{
    var error = result.Error;
    
    switch (error.Code)
    {
        case Error.Codes.Validation:
            // User input error - show validation message
            return BadRequest(error.Message);
            
        case Error.Codes.NotFound:
            // Resource not found - show 404
            return NotFound(error.Message);
            
        case Error.Codes.Conflict:
            // Business rule violation - show conflict message
            return Conflict(error.Message);
            
        case Error.Codes.Forbidden:
            // Permission denied - show 403
            return Forbid();
            
        case Error.Codes.Unexpected:
            // System error - log and show generic message
            _logger.LogError("Unexpected error: {Message}", error.Message);
            return StatusCode(500, "An unexpected error occurred");
            
        default:
            return StatusCode(500, error.Message);
    }
}
```

### Result Pipeline (Advanced)
```csharp
// Chain operations with Bind/Ensure/TapAsync
var result = await _clubService.GetByIdAsync(clubId)
    .Ensure(club => club.IsPublic, new Error(Error.Codes.Forbidden, "Club is private"))
    .TapAsync(club => _logger.LogInformation("Accessed club: {Name}", club.Name))
    .Map(club => new { club.Name, club.MembersCount });

if (result.IsSuccess)
{
    var projection = result.Value;
    // Use projection...
}
```

---

## ?? Transaction Management

All write operations use `IGenericUnitOfWork.ExecuteTransactionAsync`:

### Benefits
- **Automatic rollback** on exception
- **ACID compliance** for data integrity
- **Consistent pattern** across all services

### How It Works
```csharp
// Inside CreateClubAsync
return await _uow.ExecuteTransactionAsync<Guid>(async ctk =>
{
    // Create club
    var club = new Club { ... };
    await _clubCommand.CreateAsync(club, ctk);
    
    // Save changes
    await _uow.SaveChangesAsync(ctk);
    
    // Return result
    return Result<Guid>.Success(club.Id);
}, ct: ct);
// Transaction is automatically committed if no exception
// Transaction is automatically rolled back if exception occurs
```

### Important Notes
- **Never call `SaveChanges` outside a transaction**
- **Use provided cancellation token** (`ctk` inside transaction)
- **Return `Result<T>`** from transaction lambda
- **Don't nest transactions** (ExecuteTransactionAsync handles this)

---

## ?? Performance Considerations

### Indexes Used
- `clubs.CommunityId` - Filter by community
- `clubs.MembersCount` - Sorting by popularity
- `(clubs.CommunityId, clubs.Name)` - Unique constraint + fast lookup

### Query Optimization
- Uses `AsNoTracking()` for read queries (faster)
- Cursor pagination avoids OFFSET (scalable to large datasets)
- Leverages existing DB indexes for filtering

### Pagination Best Practices
```csharp
// ? Good: Use cursor pagination for large datasets
var cursor = new CursorRequest(Size: 20);
var result = await _clubService.SearchAsync(..., cursor, ct);

// ? Avoid: Don't use very large page sizes
var badCursor = new CursorRequest(Size: 1000); // Max is 200, clamped automatically

// ? Good: Load next page using NextCursor
if (result.IsSuccess && !string.IsNullOrEmpty(result.Value.NextCursor))
{
    var nextCursor = cursor with { Cursor = result.Value.NextCursor };
    var nextPage = await _clubService.SearchAsync(..., nextCursor, ct);
}
```

---

## ?? Testing Guide

### Unit Test Examples

#### Test SearchAsync Validation
```csharp
[Fact]
public async Task SearchAsync_WithNegativeMembersFrom_ReturnsValidationError()
{
    // Arrange
    var service = CreateService();
    var cursor = new CursorRequest();

    // Act
    var result = await service.SearchAsync(
        Guid.NewGuid(), null, null, membersFrom: -1, null, cursor);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal(Error.Codes.Validation, result.Error.Code);
    Assert.Contains("membersFrom must be >= 0", result.Error.Message);
}

[Fact]
public async Task SearchAsync_WithInvalidRange_ReturnsValidationError()
{
    // Arrange
    var service = CreateService();
    var cursor = new CursorRequest();

    // Act
    var result = await service.SearchAsync(
        Guid.NewGuid(), null, null, membersFrom: 50, membersTo: 10, cursor);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal(Error.Codes.Validation, result.Error.Code);
    Assert.Contains("membersFrom must be <= membersTo", result.Error.Message);
}
```

#### Test CreateClubAsync
```csharp
[Fact]
public async Task CreateClubAsync_WithEmptyName_ReturnsValidationError()
{
    // Arrange
    var service = CreateService();

    // Act
    var result = await service.CreateClubAsync(
        Guid.NewGuid(), Guid.NewGuid(), "", null, true);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal(Error.Codes.Validation, result.Error.Code);
    Assert.Contains("Club name is required", result.Error.Message);
}

[Fact]
public async Task CreateClubAsync_WithValidData_ReturnsClubId()
{
    // Arrange
    var service = CreateService();
    var userId = Guid.NewGuid();
    var communityId = Guid.NewGuid();

    // Act
    var result = await service.CreateClubAsync(
        userId, communityId, "Test Club", "Description", true);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotEqual(Guid.Empty, result.Value);
    
    // Verify club was created with MembersCount = 0
    var getResult = await service.GetByIdAsync(result.Value);
    Assert.True(getResult.IsSuccess);
    Assert.Equal(0, getResult.Value.MembersCount);
    Assert.Equal("Test Club", getResult.Value.Name);
}
```

#### Test GetByIdAsync
```csharp
[Fact]
public async Task GetByIdAsync_WithNonExistentId_ReturnsNotFound()
{
    // Arrange
    var service = CreateService();
    var nonExistentId = Guid.NewGuid();

    // Act
    var result = await service.GetByIdAsync(nonExistentId);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal(Error.Codes.NotFound, result.Error.Code);
    Assert.Contains(nonExistentId.ToString(), result.Error.Message);
}

[Fact]
public async Task GetByIdAsync_WithExistingId_ReturnsClub()
{
    // Arrange
    var service = CreateService();
    
    // Create a club first
    var createResult = await service.CreateClubAsync(
        Guid.NewGuid(), Guid.NewGuid(), "Test Club", null, true);
    var clubId = createResult.Value;

    // Act
    var result = await service.GetByIdAsync(clubId);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal(clubId, result.Value.Id);
    Assert.Equal("Test Club", result.Value.Name);
}
```

### Integration Test Example
```csharp
[Fact]
public async Task SearchAsync_WithFilters_ReturnsPaginatedResults()
{
    // Arrange
    var service = CreateService();
    var communityId = Guid.NewGuid();
    
    // Create test clubs
    await service.CreateClubAsync(userId, communityId, "Gaming Club", null, true);
    await service.CreateClubAsync(userId, communityId, "Chess Club", null, true);
    await service.CreateClubAsync(userId, communityId, "Private Gaming", null, false);
    
    // Act
    var cursor = new CursorRequest(Size: 10);
    var result = await service.SearchAsync(
        communityId, "gaming", isPublic: true, null, null, cursor);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Single(result.Value.Items); // Only "Gaming Club" matches
    Assert.Equal("Gaming Club", result.Value.Items[0].Name);
    Assert.Null(result.Value.NextCursor); // No more pages
}
```

---

## ?? Controller Integration Example

```csharp
[ApiController]
[Route("clubs")]
[Authorize]
public sealed class ClubsController : ControllerBase
{
    private readonly IClubService _clubService;

    public ClubsController(IClubService clubService)
    {
        _clubService = clubService;
    }

    /// <summary>
    /// Search clubs within a community
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(CursorPageResult<ClubBriefDto>), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult> SearchClubs(
        [FromQuery] Guid communityId,
        [FromQuery] string? name = null,
        [FromQuery] bool? isPublic = null,
        [FromQuery] int? membersFrom = null,
        [FromQuery] int? membersTo = null,
        [FromQuery] string? cursor = null,
        [FromQuery] int size = 20,
        CancellationToken ct = default)
    {
        size = Math.Clamp(size, 1, 200);
        
        var cursorRequest = new CursorRequest(
            Cursor: cursor,
            Size: size,
            Sort: "Id",
            Desc: true
        );

        var result = await _clubService.SearchAsync(
            communityId, name, isPublic, membersFrom, membersTo,
            cursorRequest, ct);

        return this.ToActionResult(result, successStatus: 200);
    }

    /// <summary>
    /// Create a new club
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult> CreateClub(
        [FromBody] ClubCreateRequestDto request,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _clubService.CreateClubAsync(
            userId.Value,
            request.CommunityId,
            request.Name,
            request.Description,
            request.IsPublic,
            ct);

        return this.ToActionResult(result, successStatus: 201);
    }

    /// <summary>
    /// Get club by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClubBriefDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<ActionResult> GetClubById(
        Guid id,
        CancellationToken ct = default)
    {
        var result = await _clubService.GetByIdAsync(id, ct);
        return this.ToActionResult(result, successStatus: 200);
    }
}
```

---

## ?? Summary

### Key Features
? Cursor-based pagination (scalable)
? Result pattern for consistent error handling
? Transaction management via UoW
? Automatic DI registration
? Comprehensive validation
? Performance-optimized queries
? Soft-delete support
? Audit trail auto-populated

### File Checklist
- ? `Services/Interfaces/IClubService.cs`
- ? `Services/Implementations/ClubService.cs`
- ? `Services/Common/Mapping/ClubMappers.cs`
- ? `Repositories/Interfaces/IClubQueryRepository.cs`
- ? `Repositories/Interfaces/IClubCommandRepository.cs`
- ? `Repositories/Implements/ClubQueryRepository.cs`
- ? `Repositories/Implements/ClubCommandRepository.cs`
- ? `DTOs/Clubs/ClubBriefDto.cs`
- ? `DTOs/Clubs/ClubCreateRequestDto.cs`

### Next Steps
1. Implement controller endpoints (see example above)
2. Add rate limiting policies (e.g., "ClubsRead", "ClubsCreate")
3. Add authorization policies (e.g., check community membership)
4. Write comprehensive tests
5. Add API documentation (Swagger/Scalar)

---

**Happy Coding! ??**
