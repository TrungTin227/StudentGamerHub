# ?? V?N ??: ?NG D?NG ?Ã D?NG

## Logs cho th?y:
```
The program '[9520] WebAPI.exe' has exited with code 0 (0x0).
```

? **Exit code 0** = ?ng d?ng ?ã ch?y và d?ng BÌ N?I TH??NG
? ?ng d?ng **KHÔNG ?ANG CH?Y** nên không test ???c API

---

## ?? GI?I PHÁP: CH?Y L?I ?NG D?NG

### Cách 1: Trong Visual Studio (KHUY?N NGH?)
```
Nh?n F5
```

### Cách 2: Command line
```powershell
cd C:\Users\trung\Downloads\StudentGamerHub\WebAPI
dotnet run
```

---

## ? ?NG D?NG CH?Y THÀNH CÔNG KHI B?N TH?Y:

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

### 2?? Tìm section "**Clubs**"
B?n s? th?y:
- GET `/communities/{communityId}/clubs`
- POST `/communities/{communityId}/clubs`
- GET `/communities/{communityId}/clubs/{id}`

### 3?? Test API
Click "Try it" ?? test tr?c ti?p trong Scalar UI

---

## ?? N?U V?N KHÔNG TH?Y "CLUBS" TRONG SCALAR:

### Ki?m tra OpenAPI JSON:
```
https://localhost:7227/openapi/v1.json
```

Search cho "clubs" trong JSON - n?u th?y ngh?a là API ?ã ??ng ký thành công.

### Refresh Scalar UI:
- Hard refresh: `Ctrl + Shift + R`
- Ho?c clear browser cache

---

## ?? L?U Ý:

**Hot Reload** có th? không detect ClubsController vì nó là **controller m?i**.

?ó là lý do t?i sao ph?i:
1. ? Clean cache (?ã làm)
2. ? Rebuild (?ã làm)
3. ? **Restart ?ng d?ng** (Làm bây gi?!)

---

## ?? HÃY LÀM:

1. **Nh?n F5** trong Visual Studio
2. ??i console hi?n th? "Now listening on: https://localhost:7227"
3. M? browser: `https://localhost:7227/docs`
4. Scroll xu?ng tìm "**Clubs**"
5. Th? test 1 endpoint

**N?u th?y Clubs ? SUCCESS! ??**
