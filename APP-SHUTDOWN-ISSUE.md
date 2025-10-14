# ?? L?I: ?NG D?NG T? ??NG SHUTDOWN

## V?n ?? ph�t hi?n:

?ng d?ng ?� build th�nh c�ng nh?ng **t? ??ng shutdown ngay sau khi start**:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5277
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Application is shutting down...
```

---

## ?? GI?I PH�P:

### ? C�CH 1: CH?Y TRONG VISUAL STUDIO (KHUY?N NGH?)

1. **?�ng t?t c? terminal** ?ang ch?y `dotnet run`
2. **Nh?n F5** trong Visual Studio
3. ?ng d?ng s? ch?y trong Debug mode v� kh�ng t? shutdown

---

### ? C�CH 2: CH?Y V?I --NO-LAUNCH-PROFILE

```powershell
cd C:\Users\trung\Downloads\StudentGamerHub\WebAPI
dotnet run --no-launch-profile --urls "https://localhost:7227"
```

---

### ? C�CH 3: KI?M TRA LAUNCH SETTINGS

File `WebAPI/Properties/launchSettings.json` c� th? c� v?n ??.

Ki?m tra xem c� command n�o trigger shutdown kh�ng:

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

## ?? DEBUG TH�M:

### Ki?m tra c� exception kh�ng:

Trong Visual Studio:
1. **Debug ? Windows ? Exception Settings**
2. Check "Common Language Runtime Exceptions"
3. Nh?n F5 ch?y l?i
4. N?u c� exception, debugger s? break

---

### Ki?m tra logs chi ti?t:

Th�m v�o `appsettings.Development.json`:

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

1. Click n�t "? WebAPI" (Play button m�u xanh)
2. Ho?c nh?n **F5**
3. ??i browser t? ??ng m? `https://localhost:7227`
4. Chuy?n ??n `/docs` ?? xem Scalar UI

---

## ? ?NG D?NG CH?Y ?�NG KHI:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7227
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

**V� KH�NG C�** d�ng "Application is shutting down..."

---

## ?? SAU KHI CH?Y TH�NH C�NG:

1. M? browser: `https://localhost:7227/docs`
2. T�m section "**Clubs**"
3. Test endpoints

---

## ?? L?U �:

Vi?c ?ng d?ng t? shutdown c� th? do:
- ? **Hosted service** throw exception (email dispatcher?)
- ? **Redis connection** failed
- ? **Database connection** issue
- ? **Configuration** missing
- ? **Terminal b? cancel** (Ctrl+C pressed?)

**? Ch?y trong Visual Studio s? hi?n r� l?i!**
