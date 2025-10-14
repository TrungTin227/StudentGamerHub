# Club API - Quick Reference

## ? Implementation Complete

All layers implemented: DTOs, Repositories, Service, Controller, Rate Limiting.

---

## ?? API Endpoints

### 1. Search Clubs
```
GET /communities/{communityId}/clubs
```
**Query Params**: `name`, `isPublic`, `membersFrom`, `membersTo`, `cursor`, `size`  
**Rate Limit**: 120/min  
**Returns**: `CursorPageResult<ClubBriefDto>`

### 2. Create Club
```
POST /clubs
Body: ClubCreateRequestDto
```
**Rate Limit**: 10/day  
**Returns**: `Guid` (club ID)

### 3. Get Club
```
GET /clubs/{id}
```
**Rate Limit**: 120/min  
**Returns**: `ClubBriefDto`

---

## ?? Usage

### C# Service
```csharp
// Inject
private readonly IClubService _clubService;

// Search
var result = await _clubService.SearchAsync(
    communityId, name, isPublic, membersFrom, membersTo, cursor, ct);

// Create
var createResult = await _clubService.CreateClubAsync(
    userId, communityId, name, description, isPublic, ct);

// Get
var getResult = await _clubService.GetByIdAsync(clubId, ct);
```

### HTTP Requests
```bash
# Search
GET /communities/{id}/clubs?name=gaming&size=20

# Create
POST /clubs
{ "communityId": "...", "name": "...", "isPublic": true }

# Get
GET /clubs/{id}
```

---

## ?? Rate Limits

| Policy | Limit | Applied To |
|--------|-------|------------|
| ClubsRead | 120/min | Search, Get |
| ClubsCreate | 10/day | Create |

---

## ?? Files Created

- `DTOs/Clubs/ClubBriefDto.cs`
- `DTOs/Clubs/ClubCreateRequestDto.cs`
- `Repositories/Interfaces/IClubQueryRepository.cs`
- `Repositories/Interfaces/IClubCommandRepository.cs`
- `Repositories/Implements/ClubQueryRepository.cs`
- `Repositories/Implements/ClubCommandRepository.cs`
- `Services/Interfaces/IClubService.cs`
- `Services/Implementations/ClubService.cs`
- `Services/Common/Mapping/ClubMappers.cs`
- `WebAPI/Controllers/ClubsController.cs`

---

## ?? Response Codes

- **200 OK**: Success (Search, Get)
- **201 Created**: Club created
- **400 Bad Request**: Validation error
- **401 Unauthorized**: Not authenticated
- **404 Not Found**: Club not found
- **429 Too Many Requests**: Rate limit exceeded

---

## ?? Testing

```bash
# Run app
dotnet run --project WebAPI

# Open docs
https://localhost:5001/docs

# Test endpoints in Swagger/Scalar
```

---

## ?? Documentation

- **API Guide**: `docs/ClubService-NextSteps.md`
- **Full Usage**: `docs/ClubService-UsageGuide.md`
- **Summary**: `docs/ClubService-Summary.md`
- **Checklist**: `docs/ClubService-Checklist.md`

---

**Status**: ? Production Ready  
**Build**: ? Successful  
**Docs**: https://localhost:5001/docs
