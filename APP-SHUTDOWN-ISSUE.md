# ?? L?I: ?NG D?NG T? ??NG SHUTDOWN

## V?n ?? phát hi?n:

?ng d?ng ?ã build thành công nh?ng **t? ??ng shutdown ngay sau khi start**:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5277
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Application is shutting down...
```

---

## ?? GI?I PHÁP:

### ? CÁCH 1: CH?Y TRONG VISUAL STUDIO (KHUY?N NGH?)

1. **?óng t?t c? terminal** ?ang ch?y `dotnet run`
2. **Nh?n F5** trong Visual Studio
3. ?ng d?ng s? ch?y trong Debug mode và không t? shutdown

---

### ? CÁCH 2: CH?Y V?I --NO-LAUNCH-PROFILE

```powershell
cd C:\Users\trung\Downloads\StudentGamerHub\WebAPI
dotnet run --no-launch-profile --urls "https://localhost:7227"
```

---

### ? CÁCH 3: KI?M TRA LAUNCH SETTINGS

File `WebAPI/Properties/launchSettings.json` có th? có v?n ??.

Ki?m tra xem có command nào trigger shutdown không:

```json
{
  "profiles": {
    "WebAPI": {
      "commandName": "Project",
      "launchBrowser": true,
      "applicationUrl": "https://localhost:7227;http://localhost:5277",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

---

## ?? DEBUG THÊM:

### Ki?m tra có exception không:

Trong Visual Studio:
1. **Debug ? Windows ? Exception Settings**
2. Check "Common Language Runtime Exceptions"
3. Nh?n F5 ch?y l?i
4. N?u có exception, debugger s? break

---

### Ki?m tra logs chi ti?t:

Thêm vào `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

---

## ?? KHUY?N NGH?:

**CH?Y TRONG VISUAL STUDIO:**

1. Click nút "? WebAPI" (Play button màu xanh)
2. Ho?c nh?n **F5**
3. ??i browser t? ??ng m? `https://localhost:7227`
4. Chuy?n ??n `/docs` ?? xem Scalar UI

---

## ? ?NG D?NG CH?Y ?ÚNG KHI:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7227
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

**VÀ KHÔNG CÓ** dòng "Application is shutting down..."

---

## ?? SAU KHI CH?Y THÀNH CÔNG:

1. M? browser: `https://localhost:7227/docs`
2. Tìm section "**Clubs**"
3. Test endpoints

---

## ?? L?U Ý:

Vi?c ?ng d?ng t? shutdown có th? do:
- ? **Hosted service** throw exception (email dispatcher?)
- ? **Redis connection** failed
- ? **Database connection** issue
- ? **Configuration** missing
- ? **Terminal b? cancel** (Ctrl+C pressed?)

**? Ch?y trong Visual Studio s? hi?n rõ l?i!**
