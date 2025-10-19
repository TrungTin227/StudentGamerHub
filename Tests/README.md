# StudentGamerHub Real-time Testing Tools

## Quick Start

```bash
# 1. C�i ??t dependencies
cd tests
npm install

# 2. Ch?y snapshot (in ra console)
npm run presence-snapshot

# 3. L?u ra file JSON
npm run presence-json
```

## Presence Snapshot Tool

Script ?? snapshot tr?ng th�i presence t? Redis c?a StudentGamerHub.

### C? ch? Presence

StudentGamerHub s? d?ng Redis ?? theo d�i tr?ng th�i online/offline:

- **Key presence**: `presence:{userId}` - Redis STRING v?i value "1" v� TTL
- **Key last seen**: `lastseen:{userId}` - Redis STRING v?i timestamp ISO 8601
- **TTL m?c ??nh**: 60 gi�y (c?u h�nh trong `Presence:TtlSeconds`)
- **Heartbeat**: M?i 30 gi�y client g?i heartbeat (c?u h�nh trong `Presence:HeartbeatSeconds`)

**Tr?ng th�i:**
- **Online**: Key `presence:{userId}` t?n t?i v� c� TTL > 0
- **Offline**: Key `presence:{userId}` kh�ng t?n t?i (TTL = -2)

### C�i ??t

```bash
cd tests
npm install
```

### S? d?ng

#### 1. In k?t qu? ra console

```bash
npm run presence-snapshot
```

Ho?c:

```bash
node realtime/presence-snapshot.js
```

#### 2. L?u k?t qu? ra file JSON

```bash
npm run presence-json
```

Ho?c:

```bash
node realtime/presence-snapshot.js > presence.json
```

#### 3. Override Redis connection string qua bi?n m�i tr??ng

```bash
REDIS=localhost:6379 node realtime/presence-snapshot.js
```

```bash
REDIS=redis://user:password@localhost:6379/0 node realtime/presence-snapshot.js
```

### C?u h�nh Redis

Script t? ??ng ??c chu?i k?t n?i Redis theo th? t? ?u ti�n:

1. **Bi?n m�i tr??ng `REDIS`** (?u ti�n cao nh?t)
2. **WebAPI/appsettings.Development.json** ? `Redis:ConnectionString`
3. **WebAPI/appsettings.json** ? `Redis:ConnectionString`
4. **M?c ??nh**: `localhost:6379`

### ??nh d?ng Output

Xem file m?u: [`realtime/presence-snapshot.example.json`](realtime/presence-snapshot.example.json)

```json
{
  "timestamp": "2024-01-15T10:30:45.123Z",
  "redisConnection": "localhost:6379",
  "summary": {
    "total": 15,
    "online": 8,
    "offline": 7
  },
  "users": [
    {
      "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "state": "online",
      "ttl": 45,
      "lastSeen": "2024-01-15T10:30:30.000Z"
    },
    {
      "userId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
      "state": "offline",
      "ttl": null,
      "lastSeen": "2024-01-15T09:15:22.000Z"
    }
  ]
}
```

**C�c tr??ng d? li?u:**

- `timestamp`: Th?i ?i?m ch?p snapshot
- `redisConnection`: Redis connection string ?� s? d?ng
- `summary.total`: T?ng s? user
- `summary.online`: S? user online
- `summary.offline`: S? user offline
- `users[].userId`: GUID c?a user
- `users[].state`: `"online"` ho?c `"offline"`
- `users[].ttl`: S? gi�y c�n l?i (ch? c� khi online), `null` n?u offline
- `users[].lastSeen`: Timestamp l?n cu?i seen (ISO 8601), `null` n?u ch?a c�

### V� d?

```bash
# Ch?y v� xem k?t qu?
cd tests
npm install
npm run presence-snapshot

# L?u ra file
npm run presence-json
cat presence.json | jq '.summary'

# V?i Redis t�y ch?nh
REDIS=redis://localhost:6380 node realtime/presence-snapshot.js > presence-prod.json

# L?c ch? user online
cat presence.json | jq '.users[] | select(.state == "online")'

# ??m s? user online
cat presence.json | jq '.summary.online'
```

### L?u �

- Script s? d?ng `SCAN` command n�n an to�n v?i Redis production (kh�ng block)
- Log th�ng tin ghi ra `stderr`, JSON output ghi ra `stdout`
- H? tr? Redis Cluster v� Redis Sentinel (qua ioredis)
- Script t? ??ng retry 3 l?n n?u k?t n?i Redis th?t b?i

### Troubleshooting

**L?i: Cannot find module 'ioredis'**
```bash
cd tests
npm install
```

**L?i: ENOENT: no such file or directory**
- Ki?m tra ???ng d?n appsettings.json
- Ho?c set bi?n m�i tr??ng: `REDIS=localhost:6379`

**L?i: Connection timeout**
- Ki?m tra Redis ?ang ch?y: `redis-cli ping`
- Ki?m tra connection string trong appsettings.json
