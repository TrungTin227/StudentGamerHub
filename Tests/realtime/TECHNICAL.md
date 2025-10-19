# Presence Snapshot Script - T?ng quan k? thu?t

## Phân tích C? ch? Presence trong StudentGamerHub

### 1. Ki?n trúc Redis

#### Key Structure
```
presence:{userId}    ? STRING "1" v?i TTL (60s m?c ??nh)
lastseen:{userId}    ? STRING v?i ISO timestamp
```

#### Flow Ho?t ??ng

**Server-side (PresenceHub.cs & PresenceService.cs):**
```csharp
// Heartbeat ???c g?i t? SignalR client m?i 30s
public async Task Heartbeat()
{
    var ttl = TimeSpan.FromSeconds(60); // _options.TtlSeconds
    var timestamp = DateTime.UtcNow.ToString("O");
    
    await Task.WhenAll(
        db.StringSetAsync($"presence:{userId}", "1", ttl),
        db.StringSetAsync($"lastseen:{userId}", timestamp)
    );
}
```

**Xác ??nh tr?ng thái:**
- **Online**: Key `presence:{userId}` t?n t?i (TTL > 0)
- **Offline**: Key `presence:{userId}` không t?n t?i (TTL = -2)

### 2. Script Node.js (presence-snapshot.js)

#### Công vi?c th?c hi?n:
1. ??c Redis connection string t? appsettings.json (ho?c env REDIS)
2. SCAN t?t c? keys `presence:*` và `lastseen:*`
3. Dùng Redis Pipeline ?? l?y TTL và lastSeen cho m?i user
4. Xác ??nh tr?ng thái online/offline d?a trên TTL
5. Output JSON v?i summary và chi ti?t t?ng user

#### T?i ?u hóa:
- **SCAN thay vì KEYS**: An toàn cho production, không block Redis
- **Pipeline**: G?p nhi?u command thành 1 round-trip, gi?m network latency
- **Lazy connection**: Ch? connect khi c?n, tránh waste resources
- **Retry strategy**: T? ??ng retry 3 l?n v?i exponential backoff

#### X? lý Edge Cases:
- User có `lastseen` nh?ng không có `presence` ? offline
- User có `presence` nh?ng không có `lastseen` ? online (rare, có th? x?y ra n?u Redis restart)
- TTL = -1: Key t?n t?i nh?ng không có TTL ? treat as online
- TTL = -2: Key không t?n t?i ? offline

### 3. Output Format

```typescript
interface PresenceSnapshot {
  timestamp: string;           // ISO 8601
  redisConnection: string;     // Connection string ?ã dùng
  summary: {
    total: number;             // T?ng s? user
    online: number;            // User có presence key
    offline: number;           // User không có presence key
  };
  users: Array<{
    userId: string;            // GUID
    state: 'online' | 'offline';
    ttl: number | null;        // S? giây còn l?i (null n?u offline)
    lastSeen: string | null;   // ISO timestamp (null n?u ch?a có)
  }>;
}
```

### 4. Các tr??ng h?p s? d?ng

#### Monitoring & Debugging
```bash
# Snapshot hi?n t?i
npm run presence-snapshot

# Tracking theo th?i gian
while true; do 
  node realtime/presence-snapshot.js > "presence-$(date +%s).json"
  sleep 60
done
```

#### Analytics
```bash
# Xem user online
cat presence.json | jq '.users[] | select(.state == "online") | .userId'

# ??m online theo gi?
cat presence.json | jq '.summary'

# User s?p offline (TTL < 10s)
cat presence.json | jq '.users[] | select(.ttl != null and .ttl < 10)'
```

#### Testing
```bash
# Ki?m tra presence sau khi deploy
npm run presence-json
diff presence-before.json presence-after.json
```

### 5. C?u hình Redis Connection

Priority order:
1. **Environment variable**: `REDIS=localhost:6379`
2. **appsettings.Development.json**: `Redis.ConnectionString`
3. **appsettings.json**: `Redis.ConnectionString`
4. **Default fallback**: `localhost:6379`

H? tr? các format:
```bash
# Simple
REDIS=localhost:6379

# With password
REDIS=localhost:6379,password=mypassword

# Redis URL format
REDIS=redis://user:password@localhost:6379/0

# Sentinel
REDIS=sentinel1:26379,sentinel2:26379,serviceName=mymaster

# Cluster
REDIS=node1:6379,node2:6379,node3:6379
```

### 6. Error Handling

Script handle các l?i:
- **Connection failed**: Retry 3 l?n, sau ?ó exit v?i code 1
- **JSON parse error**: Skip file và fallback sang file config khác
- **Pipeline error**: Individual error handling cho t?ng command
- **File not found**: Fallback sang default connection string

### 7. Performance Considerations

**?? ph?c t?p:**
- SCAN: O(N) v?i N là t?ng s? keys trong Redis
- Pipeline: O(K) v?i K là s? user c?n query
- Total: O(N + K)

**Memory:**
- Node.js heap: ~50MB cho 10,000 users
- Redis bandwidth: ~1KB per user (TTL + lastSeen)

**Th?i gian ch?y:**
- 1,000 users: ~500ms
- 10,000 users: ~2s
- 100,000 users: ~15s

### 8. Best Practices

#### Development
```bash
# Local testing
REDIS=localhost:6379 npm run presence-snapshot

# With Docker
docker run --rm redis redis-cli -h host.docker.internal -p 6379 KEYS "presence:*"
```

#### Production
```bash
# Cron job m?i 5 phút
*/5 * * * * cd /app/tests && npm run presence-json > /var/log/presence/presence-$(date +\%Y\%m\%d-\%H\%M).json

# Monitoring alert
ONLINE_COUNT=$(cat presence.json | jq '.summary.online')
if [ $ONLINE_COUNT -gt 10000 ]; then
  echo "High online users: $ONLINE_COUNT" | mail -s "Alert" admin@example.com
fi
```

#### CI/CD
```yaml
# GitHub Actions
- name: Check presence after deploy
  run: |
    cd tests
    npm install
    npm run presence-json
    echo "Online users: $(cat presence.json | jq '.summary.online')"
```

### 9. Troubleshooting

#### Script không ch?y
```bash
# Check Node.js
node --version  # >= 14.0

# Check dependencies
cd tests && npm install

# Check Redis connection
redis-cli -h localhost -p 6379 PING
```

#### Không tìm th?y user
```bash
# Check keys trong Redis
redis-cli KEYS "presence:*" | wc -l
redis-cli KEYS "lastseen:*" | wc -l

# Check TTL
redis-cli TTL "presence:{userId}"
redis-cli GET "lastseen:{userId}"
```

#### JSON parsing error
- Ki?m tra appsettings.json có valid không
- Script ?ã handle comments (//) trong JSON
- Set bi?n môi tr??ng REDIS ?? bypass config file

### 10. Future Enhancements

Có th? m? r?ng thêm:
- [ ] Export sang CSV format
- [ ] Grafana dashboard integration
- [ ] WebSocket streaming mode (real-time updates)
- [ ] Historical data tracking (time-series)
- [ ] Alerting khi có anomaly (sudden drop/spike)
- [ ] Integration v?i Prometheus metrics
- [ ] Support Redis Cluster topology discovery
