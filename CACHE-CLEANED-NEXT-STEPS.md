# ? CACHE ?Ã ???C CLEAN - H??NG D?N TI?P THEO

## ?? Nh?ng gì ?ã làm:

? **Shutdown build servers** - D?ng t?t c? MSBuild và compiler processes  
? **Clean solution** - Xóa artifacts c?  
? **Delete bin/obj folders** - Xóa s?ch t?t c? compiled files  
? **Restore packages** - T?i l?i NuGet packages  
? **Rebuild solution** - Build l?i t? ??u (no-incremental)

## ?? BÂY GI? HÃY LÀM:

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

### B??C 3: Tìm section "Clubs"
B?n s? th?y **3 endpoints**:
- ?? **GET** `/communities/{communityId}/clubs` - Search clubs
- ? **POST** `/communities/{communityId}/clubs` - Create club  
- ?? **GET** `/communities/{communityId}/clubs/{id}` - Get club by ID

---

## ?? Test API Clubs:

### Option 1: Dùng Scalar UI (Recommended)
1. M? `https://localhost:7227/docs`
2. Click vào endpoint "Clubs"
3. Click "Try it" ?? test tr?c ti?p

### Option 2: Dùng .http file
1. M? file `test-clubs-simple.http`
2. Làm theo h??ng d?n t?ng b??c
3. Thay token và communityId vào

### Option 3: Dùng curl/Postman
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

## ?? N?u v?n không th?y Clubs trong Scalar:

### Debug Checklist:

1. **Ki?m tra OpenAPI JSON**:
   ```
   https://localhost:7227/openapi/v1.json
   ```
   Tìm ki?m "clubs" - ph?i có 3 paths

2. **Ki?m tra console log**:
   - M? Output window trong Visual Studio
   - Ch?n "Debug" t? dropdown
   - Tìm errors liên quan ??n ClubsController

3. **Set breakpoint**:
   - M? `WebAPI/Controllers/ClubsController.cs`
   - Set breakpoint t?i constructor
   - F5 ch?y l?i
   - N?u breakpoint không hit ? DI issue

4. **Verify DI registration**:
   Thêm vào `Program.cs` tr??c `app.Run()`:
   ```csharp
   var clubService = app.Services.GetService<IClubService>();
   Console.WriteLine($"IClubService registered: {clubService != null}");
   ```

---

## ?? L?nh nhanh cho l?n sau:

Thay vì clean th? công, ch?y script:
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

## ?? Ki?n trúc Clubs API:

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

T?t c? các component ?ã ???c implement ?

---

## ? N?u v?n g?p v?n ??:

1. Check appsettings.json - ConnectionString ?úng ch?a?
2. Check PostgreSQL ?ang ch?y ch?a?
3. Check migrations ?ã apply ch?a?
4. Share console error log ?? debug

---

## ?? Khi thành công:

B?n s? th?y trong Scalar:
- Section "Clubs" 
- 3 endpoints v?i ??y ?? documentation
- "Try it" button ?? test tr?c ti?p
- Request/Response examples

**Good luck! ??**
