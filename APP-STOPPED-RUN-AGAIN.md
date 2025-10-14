# ?? V?N ??: ?NG D?NG ?� D?NG

## Logs cho th?y:
```
The program '[9520] WebAPI.exe' has exited with code 0 (0x0).
```

? **Exit code 0** = ?ng d?ng ?� ch?y v� d?ng B� N?I TH??NG
? ?ng d?ng **KH�NG ?ANG CH?Y** n�n kh�ng test ???c API

---

## ?? GI?I PH�P: CH?Y L?I ?NG D?NG

### C�ch 1: Trong Visual Studio (KHUY?N NGH?)
```
Nh?n F5
```

### C�ch 2: Command line
```powershell
cd C:\Users\trung\Downloads\StudentGamerHub\WebAPI
dotnet run
```

---

## ? ?NG D?NG CH?Y TH�NH C�NG KHI B?N TH?Y:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7227
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shutdown.
```

---

## ?? SAU KHI ?NG D?NG CH?Y:

### 1?? M? Scalar UI
```
https://localhost:7227/docs
```

### 2?? T�m section "**Clubs**"
B?n s? th?y:
- GET `/communities/{communityId}/clubs`
- POST `/communities/{communityId}/clubs`
- GET `/communities/{communityId}/clubs/{id}`

### 3?? Test API
Click "Try it" ?? test tr?c ti?p trong Scalar UI

---

## ?? N?U V?N KH�NG TH?Y "CLUBS" TRONG SCALAR:

### Ki?m tra OpenAPI JSON:
```
https://localhost:7227/openapi/v1.json
```

Search cho "clubs" trong JSON - n?u th?y ngh?a l� API ?� ??ng k� th�nh c�ng.

### Refresh Scalar UI:
- Hard refresh: `Ctrl + Shift + R`
- Ho?c clear browser cache

---

## ?? L?U �:

**Hot Reload** c� th? kh�ng detect ClubsController v� n� l� **controller m?i**.

?� l� l� do t?i sao ph?i:
1. ? Clean cache (?� l�m)
2. ? Rebuild (?� l�m)
3. ? **Restart ?ng d?ng** (L�m b�y gi?!)

---

## ?? H�Y L�M:

1. **Nh?n F5** trong Visual Studio
2. ??i console hi?n th? "Now listening on: https://localhost:7227"
3. M? browser: `https://localhost:7227/docs`
4. Scroll xu?ng t�m "**Clubs**"
5. Th? test 1 endpoint

**N?u th?y Clubs ? SUCCESS! ??**
