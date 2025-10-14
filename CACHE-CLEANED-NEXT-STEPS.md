# ? CACHE ?� ???C CLEAN - H??NG D?N TI?P THEO

## ?? Nh?ng g� ?� l�m:

? **Shutdown build servers** - D?ng t?t c? MSBuild v� compiler processes  
? **Clean solution** - X�a artifacts c?  
? **Delete bin/obj folders** - X�a s?ch t?t c? compiled files  
? **Restore packages** - T?i l?i NuGet packages  
? **Rebuild solution** - Build l?i t? ??u (no-incremental)

## ?? B�Y GI? H�Y L�M:

### B??C 1: Ch?y ?ng d?ng
```
Nh?n F5 trong Visual Studio
ho?c
cd WebAPI && dotnet run
```

### B??C 2: Ki?m tra Scalar UI
M? browser:
```
https://localhost:7227/docs
```

### B??C 3: T�m section "Clubs"
B?n s? th?y **3 endpoints**:
- ?? **GET** `/communities/{communityId}/clubs` - Search clubs
- ? **POST** `/communities/{communityId}/clubs` - Create club  
- ?? **GET** `/communities/{communityId}/clubs/{id}` - Get club by ID

---

## ?? Test API Clubs:

### Option 1: D�ng Scalar UI (Recommended)
1. M? `https://localhost:7227/docs`
2. Click v�o endpoint "Clubs"
3. Click "Try it" ?? test tr?c ti?p

### Option 2: D�ng .http file
1. M? file `test-clubs-simple.http`
2. L�m theo h??ng d?n t?ng b??c
3. Thay token v� communityId v�o

### Option 3: D�ng curl/Postman
```bash
# 1. Login
curl -k -X POST https://localhost:7227/auth/login \
  -H "Content-Type: application/json" \
  -d '{"Email":"admin@example.com","Password":"Admin@123"}'

# 2. Get communities
curl -k https://localhost:7227/communities \
  -H "Authorization: Bearer YOUR_TOKEN"

# 3. Get clubs
curl -k https://localhost:7227/communities/YOUR_COMMUNITY_ID/clubs \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

## ?? N?u v?n kh�ng th?y Clubs trong Scalar:

### Debug Checklist:

1. **Ki?m tra OpenAPI JSON**:
   ```
   https://localhost:7227/openapi/v1.json
   ```
   T�m ki?m "clubs" - ph?i c� 3 paths

2. **Ki?m tra console log**:
   - M? Output window trong Visual Studio
   - Ch?n "Debug" t? dropdown
   - T�m errors li�n quan ??n ClubsController

3. **Set breakpoint**:
   - M? `WebAPI/Controllers/ClubsController.cs`
   - Set breakpoint t?i constructor
   - F5 ch?y l?i
   - N?u breakpoint kh�ng hit ? DI issue

4. **Verify DI registration**:
   Th�m v�o `Program.cs` tr??c `app.Run()`:
   ```csharp
   var clubService = app.Services.GetService<IClubService>();
   Console.WriteLine($"IClubService registered: {clubService != null}");
   ```

---

## ?? L?nh nhanh cho l?n sau:

Thay v� clean th? c�ng, ch?y script:
```powershell
.\clean-and-rebuild.ps1
```

Ho?c trong Visual Studio:
```
1. Shift+F5 (Stop)
2. Ctrl+Shift+B (Rebuild)
3. F5 (Run)
```

---

## ?? Ki?n tr�c Clubs API:

```
ClubsController (WebAPI)
    ?
IClubService (Services.Interfaces)
    ?
ClubService (Services.Implementations)
    ?
IClubQueryRepository + IClubCommandRepository
    ?
ClubQueryRepository + ClubCommandRepository (Repositories)
    ?
AppDbContext (EF Core)
    ?
PostgreSQL Database
```

T?t c? c�c component ?� ???c implement ?

---

## ? N?u v?n g?p v?n ??:

1. Check appsettings.json - ConnectionString ?�ng ch?a?
2. Check PostgreSQL ?ang ch?y ch?a?
3. Check migrations ?� apply ch?a?
4. Share console error log ?? debug

---

## ?? Khi th�nh c�ng:

B?n s? th?y trong Scalar:
- Section "Clubs" 
- 3 endpoints v?i ??y ?? documentation
- "Try it" button ?? test tr?c ti?p
- Request/Response examples

**Good luck! ??**
