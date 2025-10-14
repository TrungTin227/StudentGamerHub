# Clubs API Troubleshooting Guide

## Problem: "v?n không có gì api clubs" (No response from Clubs API)

### Root Cause
The `.http` test file is using undefined variables: `{{token}}`, `{{communityId}}`, and `{{clubId}}`.

### Solution

#### Option 1: Use the Updated Test File with Manual Values

1. Open `test-clubs-api.http`
2. Follow the steps in order:
   - First, login to get your authentication token
   - Replace `YOUR_TOKEN_HERE` with the actual token from login response
   - Replace `YOUR_COMMUNITY_ID_HERE` with an actual Community ID
   - Replace `YOUR_CLUB_ID_HERE` with a Club ID after creating one

#### Option 2: Use Variable-Based Test File (Recommended)

1. Open `test-clubs-api-with-variables.http`
2. Update the login credentials in STEP 1
3. Run requests sequentially - variables will be auto-populated
4. Each request extracts values for the next request

### Verification Steps

#### 1. Check if the API is running
```bash
curl -k https://localhost:7227/openapi/v1.json
```

#### 2. Get a test token
```bash
curl -k -X POST https://localhost:7227/auth/login \
  -H "Content-Type: application/json" \
  -d '{"Email":"admin@example.com","Password":"Admin@123"}'
```

#### 3. List communities to get a valid communityId
```bash
curl -k -X GET "https://localhost:7227/communities?page=1&size=10" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

#### 4. Test Clubs API
```bash
curl -k -X GET "https://localhost:7227/communities/YOUR_COMMUNITY_ID/clubs" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

### Common Issues

#### Issue 1: 401 Unauthorized
**Cause**: Missing or invalid authentication token
**Solution**: 
- Ensure you're logged in
- Check token hasn't expired (tokens typically expire after 15-60 minutes)
- Re-login to get a fresh token

#### Issue 2: 404 Not Found
**Cause**: Invalid Community ID
**Solution**:
- List communities first: `GET /communities`
- Use a valid Community ID from the response

#### Issue 3: 429 Too Many Requests
**Cause**: Rate limit exceeded
- Read operations: 120 requests/minute
- Create operations: 10 requests/day
**Solution**: Wait for the rate limit window to reset

#### Issue 4: No communities exist
**Cause**: Database is empty
**Solution**: Create a community first:
```bash
curl -k -X POST https://localhost:7227/communities \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"Name":"Test Community","Description":"Test","IsPublic":true,"School":"Test University"}'
```

### API Endpoints Summary

| Method | Endpoint | Description | Rate Limit |
|--------|----------|-------------|------------|
| GET | `/communities/{communityId}/clubs` | Search/list clubs | 120/min |
| GET | `/communities/{communityId}/clubs/{clubId}` | Get club by ID | 120/min |
| POST | `/communities/{communityId}/clubs` | Create new club | 10/day |

### Request Examples

#### Search Clubs with Filters
```http
GET /communities/{communityId}/clubs?name=gaming&isPublic=true&membersFrom=10&membersTo=100&size=20
```

#### Create a Club
```http
POST /communities/{communityId}/clubs
Content-Type: application/json

{
  "Name": "My Gaming Club",
  "Description": "Description here",
  "IsPublic": true
}
```

### Response Examples

#### Successful Search
```json
{
  "Items": [
    {
      "Id": "guid-here",
      "CommunityId": "guid-here",
      "Name": "Gaming Club",
      "Description": "...",
      "IsPublic": true,
      "MembersCount": 5,
      "CreatedAtUtc": "2024-01-01T00:00:00Z"
    }
  ],
  "NextCursor": "cursor-string-or-null",
  "PrevCursor": null,
  "Size": 20,
  "Sort": "Id",
  "Desc": true
}
```

#### Successful Create
```json
"club-guid-here"
```

### Debugging Tips

1. **Check Visual Studio Output Window**:
   - View ? Output
   - Select "Debug" from dropdown
   - Look for exceptions or error messages

2. **Check Application Logs**:
   - Look for startup messages
   - Check for database connection issues
   - Verify ClubService is registered

3. **Verify Database**:
   ```sql
   SELECT * FROM "Communities";
   SELECT * FROM "Clubs";
   ```

4. **Test with Swagger/Scalar UI**:
   - Navigate to `https://localhost:7227/docs`
   - Use the interactive API documentation

### Architecture Verification

? **ClubsController** exists at `WebAPI/Controllers/ClubsController.cs`
? **ClubService** exists at `Services/Implementations/ClubService.cs`
? **IClubService** is auto-registered by convention (ends with "Service")
? **Rate limiting** configured for ClubsRead (120/min) and ClubsCreate (10/day)
? **Authorization** required on all endpoints
? **Routes** mapped: `/communities/{communityId}/clubs`

### Next Steps if Still Not Working

1. Set a breakpoint in `ClubsController.SearchClubs` method
2. Run the application in Debug mode (F5)
3. Send a request and see if breakpoint is hit
4. Check request URL, headers, and authentication
5. Verify EF Core is loading Clubs from database

### Support

If you're still experiencing issues:
1. Check the console output for any startup errors
2. Verify PostgreSQL is running and accessible
3. Ensure migrations have been applied
4. Check that the JWT authentication middleware is configured correctly
