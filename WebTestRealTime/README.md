# WebTestRealTime - Chat Testing Application

## Mô t?
WebTestRealTime là ?ng d?ng Razor Pages ?? test ch?c n?ng realtime chat c?a Student Gamer Hub thông qua SignalR.

## Yêu c?u
1. **WebAPI** ph?i ?ang ch?y trên `https://localhost:7227`
2. **Redis** ph?i ?ang ch?y (cho chat history và rate limiting)
3. **.NET 9** SDK

## C?u hình

### appsettings.Development.json
```json
{
  "ChatTest": {
    "BaseUrl": "https://localhost:7227",
    "Email": "realtime-a@studentgamerhub.local",
    "Password": "Password123!"
  }
}
```

B?n có th? thay ??i:
- `BaseUrl`: URL c?a WebAPI
- `Email`: Email test user (t? ??ng t?o n?u ch?a có)
- `Password`: M?t kh?u test user

## Ch?y ?ng d?ng

### 1. Kh?i ??ng Redis
```bash
# Docker
docker run -d -p 6379:6379 redis

# Ho?c ch?y Redis local
redis-server
```

### 2. Kh?i ??ng WebAPI
```bash
cd WebAPI
dotnet run
```

### 3. Kh?i ??ng WebTestRealTime
```bash
cd WebTestRealTime
dotnet run
```

Truy c?p: `https://localhost:5001` ho?c `http://localhost:5000`

## H??ng d?n s? d?ng

### Test Direct Message (DM)

1. M? trang Chat: `/Chat`
2. Click **Login** ?? xác th?c
3. Nh?p **Other User Id** (GUID c?a user khác)
4. Click **Join DM** ?? tham gia kênh DM
5. Nh?p message và click **Send DM**
6. Click **Load DM History** ?? xem l?ch s?

### Test Room Chat

1. Login nh? trên
2. Nh?p **Room Id** (GUID c?a room)
3. Click **Join Room**
4. Nh?p message và click **Send To Room**

### Test v?i nhi?u user

?? test realtime gi?a 2 user:

1. M? 2 browser khác nhau (ho?c 1 normal + 1 incognito)
2. User A: Login v?i `realtime-a@studentgamerhub.local`
3. User B: Login v?i `realtime-b@studentgamerhub.local`
4. L?y User ID c?a User B t? log sau khi login
5. User A: Nh?p User B ID vào "Other User Id"
6. C? 2 user: Click **Join DM**
7. G?i message qua l?i

## Các tính n?ng

### Connection Status
- **Green badge**: ?ã k?t n?i
- **Yellow badge**: ?ang k?t n?i/reconnecting
- **Red badge**: Ng?t k?t n?i

### Log Window
- Hi?n th? t?t c? event và message
- Auto-scroll to bottom
- Button **Clear Log** ?? xóa log
- Timestamp cho m?i entry

### Message Format
```
[MSG 10:30:45] From: {userId} ? To: {userId/roomId}: {text}
```

## API Endpoints ???c s? d?ng

- `POST /api/auth/login` - ??ng nh?p
- `GET /api/auth/me` - L?y thông tin user
- SignalR Hub `/ws/chat`:
  - `SendDm(toUserId, text)` - G?i DM
  - `SendToRoom(roomId, text)` - G?i room message
  - `JoinChannels(channels[])` - Join channels
  - `LoadHistory(channel, afterId, take)` - Load history
  - Event `msg` - Nh?n message
  - Event `history` - Nh?n history

## Troubleshooting

### L?i "Cannot connect to SignalR"
- Ki?m tra WebAPI ?ang ch?y
- Ki?m tra URL trong appsettings.json
- Ki?m tra CORS settings trong WebAPI

### L?i "Login failed"
- Ki?m tra database connection
- Ki?m tra user credentials
- User s? t? ??ng ???c t?o n?u ch?a t?n t?i

### Không nh?n ???c message
- Ki?m tra Redis ?ang ch?y
- Ki?m tra c? 2 user ?ã join cùng channel
- Xem log ?? debug

### Rate limit errors
- ??i 1 phút r?i th? l?i
- Ki?m tra rate limit config trong WebAPI

## Development

### Project Structure
```
WebTestRealTime/
??? Pages/
?   ??? Chat.cshtml          # Chat test page
?   ??? Chat.cshtml.cs       # Page model
?   ??? Index.cshtml         # Home page
?   ??? Shared/
?       ??? _Layout.cshtml   # Layout with SignalR
??? wwwroot/
?   ??? css/
?   ??? js/
?   ??? lib/                 # SignalR client library
??? appsettings.json
??? appsettings.Development.json
```

### Dependencies
- SignalR Client: `@microsoft/signalr@7.0.14` (t? CDN)
- Bootstrap 5
- jQuery (cho Bootstrap)

## M? r?ng

### Thêm test user m?i
Ch?nh s?a `appsettings.Development.json` ho?c t?o file m?i.

### Test v?i room có s?n
Set environment variable `ROOM_ID`:
```bash
set ROOM_ID=your-room-guid
dotnet run
```

### Thêm test automation
Xem folder `Tests/realtime/` cho các script test t? ??ng.
