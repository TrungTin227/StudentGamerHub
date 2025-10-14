# Club Service - API Integration Guide

## ✅ Implementation Complete

The Club Service is fully implemented and ready to use.

---

## 🚀 Quick Start

### API Endpoints

**Base Route**: `https://localhost:5001`

#### 1. Search Clubs
```http
GET /communities/{communityId}/clubs?name=gaming&isPublic=true&size=20
Authorization: Bearer {token}
```

**Parameters:**
- `communityId` (path, required): Community ID
- `name` (query, optional): Filter by name (partial match)
- `isPublic` (query, optional): Filter by visibility (true/false)
- `membersFrom` (query, optional): Min members count
- `membersTo` (query, optional): Max members count
- `cursor` (query, optional): Pagination cursor
- `size` (query, optional): Page size (default: 20, max: 200)

**Response:**
```json
{
  "Items": [
    {
      "Id": "guid",
      "CommunityId": "guid",
      "Name": "Gaming Club",
      "IsPublic": true,
      "MembersCount": 25,
      "Description": "For gamers"
    }
  ],
  "NextCursor": "base64token",
  "Size": 20
}
```

**Rate Limit:** 120 requests/minute per user

---

#### 2. Create Club
```http
POST /clubs
Authorization: Bearer {token}
Content-Type: application/json

{
  "communityId": "123e4567-e89b-12d3-a456-426614174000",
  "name": "My Club",
  "description": "Optional description",
  "isPublic": true
}
```

**Response:**
```json
"123e4567-e89b-12d3-a456-426614174001"
```

**Rate Limit:** 10 requests/day per user

---

#### 3. Get Club by ID
```http
GET /clubs/{id}
Authorization: Bearer {token}
```

**Response:**
```json
{
  "Id": "guid",
  "CommunityId": "guid",
  "Name": "Gaming Club",
  "IsPublic": true,
  "MembersCount": 25,
  "Description": "For gamers"
}
```

**Rate Limit:** 120 requests/minute per user

---

## 📊 Rate Limiting

| Policy | Limit | Period |
|--------|-------|--------|
| ClubsRead | 120 requests | per minute |
| ClubsCreate | 10 requests | per day |

**429 Response** when limit exceeded:
```json
{
  "type": "https://httpstatuses.com/429",
  "title": "Too Many Requests",
  "status": 429
}
```

---

## 🔧 Testing

### Using Swagger/Scalar

1. Run the application:
   ```bash
   dotnet run --project WebAPI
   ```

2. Open Swagger UI:
   ```
   https://localhost:5001/swagger
   ```
   or Scalar:
   ```
   https://localhost:5001/docs
   ```

3. Authorize using JWT token

4. Test the 3 Club endpoints

---

## 📝 Code Examples

### C# Client
```csharp
// Inject service
private readonly IClubService _clubService;

// Search clubs
var cursor = new CursorRequest(Size: 20);
var result = await _clubService.SearchAsync(
    communityId, name: "gaming", isPublic: true,
    membersFrom: 10, membersTo: 50, cursor, ct);

if (result.IsSuccess)
{
    var clubs = result.Value.Items;
    var nextCursor = result.Value.NextCursor;
}

// Create club
var createResult = await _clubService.CreateClubAsync(
    userId, communityId, "My Club", "Description", true, ct);

if (createResult.IsSuccess)
{
    var clubId = createResult.Value;
}

// Get club
var getResult = await _clubService.GetByIdAsync(clubId, ct);
```

### JavaScript/TypeScript
```typescript
// Search clubs
const response = await fetch(
  `/communities/${communityId}/clubs?name=gaming&size=20`,
  { headers: { Authorization: `Bearer ${token}` } }
);
const data = await response.json();

// Create club
const createResponse = await fetch('/clubs', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    communityId: '...',
    name: 'My Club',
    description: 'Optional',
    isPublic: true
  })
});
const clubId = await createResponse.json();

// Get club
const getResponse = await fetch(`/clubs/${clubId}`, {
  headers: { Authorization: `Bearer ${token}` }
});
const club = await getResponse.json();
```

---

## 🛠️ Architecture

### Components
- **Service**: `IClubService` / `ClubService`
- **Repositories**: `IClubQueryRepository`, `IClubCommandRepository`
- **DTOs**: `ClubBriefDto`, `ClubCreateRequestDto`
- **Controller**: `ClubsController`

### Flow
```
HTTP Request → Controller → Service → Repository → Database
                  ↓
            ToActionResult (maps Result<T> to HTTP response)
```

### Error Codes
- **200 OK**: Success
- **201 Created**: Club created
- **400 Bad Request**: Validation error
- **401 Unauthorized**: Not authenticated
- **404 Not Found**: Club not found
- **429 Too Many Requests**: Rate limit exceeded

---

## 📚 More Documentation

- **Detailed Usage Guide**: `docs/ClubService-UsageGuide.md`
- **Implementation Summary**: `docs/ClubService-Summary.md`
- **Checklist**: `docs/ClubService-Checklist.md`

---

## ✅ Deployment Checklist

- [x] Service layer implemented
- [x] Repository layer implemented
- [x] Controller created
- [x] Rate limiting configured
- [x] API documented
- [ ] Unit tests written
- [ ] Integration tests written
- [ ] Frontend integrated

---

**Status**: ✅ Ready for Use
**Documentation**: https://localhost:5001/docs
