# KI?M TRA CLUBS API NHANH

## B??c 1: D?ng ?ng d?ng
- Nh?n Shift+F5 trong Visual Studio

## B??c 2: Clean & Rebuild
```powershell
cd C:\Users\trung\Downloads\StudentGamerHub
dotnet clean
dotnet build
```

## B??c 3: Ch?y l?i
- Nh?n F5

## B??c 4: Ki?m tra OpenAPI JSON
M? browser ho?c Postman:
```
https://localhost:7227/openapi/v1.json
```

T�m ki?m "clubs" trong JSON - b?n s? th?y:
```json
{
  "paths": {
    "/communities/{communityId}/clubs": {
      "get": { ... },
      "post": { ... }
    },
    "/communities/{communityId}/clubs/{id}": {
      "get": { ... }
    }
  }
}
```

## B??c 5: Ki?m tra Scalar UI
```
https://localhost:7227/docs
```

B?n s? th?y section "Clubs" v?i 3 endpoints:
- GET /communities/{communityId}/clubs - Search clubs
- POST /communities/{communityId}/clubs - Create club
- GET /communities/{communityId}/clubs/{id} - Get club by ID

## N?u v?n kh�ng th?y:

### Ki?m tra console log khi startup:
T�m d�ng t??ng t?:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7227
```

### Ki?m tra Controller c� ???c load kh�ng:
Set breakpoint t?i constructor c?a ClubsController:
```csharp
public ClubsController(IClubService clubService)
{
    _clubService = clubService ?? throw new ArgumentNullException(nameof(clubService));
    // <-- SET BREAKPOINT HERE
}
```

N?u breakpoint kh�ng hit ? Controller kh�ng ???c DI container t?o ? C� l?i dependency injection

### Debug DI:
Th�m log v�o Program.cs tr??c app.Run():
```csharp
var clubService = app.Services.GetService<IClubService>();
Console.WriteLine($"ClubService registered: {clubService != null}");
```
