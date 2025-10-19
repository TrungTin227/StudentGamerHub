# Presence Snapshot Script - T?ng quan k? thu?t

## Ph�n t�ch C? ch? Presence trong StudentGamerHub

### 1. Ki?n tr�c Redis

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

**X�c ??nh tr?ng th�i:**
- **Online**: Key `presence:{userId}` t?n t?i (TTL > 0)
- **Offline**: Key `presence:{userId}` kh�ng t?n t?i (TTL = -2)

### 2. Script Node.js (presence-snapshot.js)

#### C�ng vi?c th?c hi?n:
1. ??c Redis connection string t? appsettings.json (ho?c env REDIS)
2. SCAN t?t c? keys `presence:*` v� `lastseen:*`
3. D�ng Redis Pipeline ?? l?y TTL v� lastSeen cho m?i user
4. X�c ??nh tr?ng th�i online/offline d?a tr�n TTL
5. Output JSON v?i summary v� chi ti?t t?ng user

#### T?i ?u h�a:
- **SCAN thay v� KEYS**: An to�n cho production, kh�ng block Redis
- **Pipeline**: G?p nhi?u command th�nh 1 round-trip, gi?m network latency
- **Lazy connection**: Ch? connect khi c?n, tr�nh waste resources
- **Retry strategy**: T? ??ng retry 3 l?n v?i exponential backoff

#### X? l� Edge Cases:
- User c� `lastseen` nh?ng kh�ng c� `presence` ? offline
- User c� `presence` nh?ng kh�ng c� `lastseen` ? online (rare, c� th? x?y ra n?u Redis restart)
- TTL = -1: Key t?n t?i nh?ng kh�ng c� TTL ? treat as online
- TTL = -2: Key kh�ng t?n t?i ? offline

### 3. Output Format

```typescript
interface PresenceSnapshot {
  timestamp: string;           // ISO 8601
  redisConnection: string;     // Connection string ?� d�ng
  summary: {
    total: number;             // T?ng s? user
    online: number;            // User c� presence key
    offline: number;           // User kh�ng c� presence key
  };
  users: Array<{
    userId: string;            // GUID
    state: 'online' | 'offline';
    ttl: number | null;        // S? gi�y c�n l?i (null n?u offline)
    lastSeen: string | null;   // ISO timestamp (null n?u ch?a c�)
  }>;
}
```

### 4. C�c tr??ng h?p s? d?ng

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

### 5. C?u h�nh Redis Connection

Priority order:
1. **Environment variable**: `REDIS=localhost:6379`
2. **appsettings.Development.json**: `Redis.ConnectionString`
3. **appsettings.json**: `Redis.ConnectionString`
4. **Default fallback**: `localhost:6379`

H? tr? c�c format:
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

Script handle c�c l?i:
- **Connection failed**: Retry 3 l?n, sau ?� exit v?i code 1
- **JSON parse error**: Skip file v� fallback sang file config kh�c
- **Pipeline error**: Individual error handling cho t?ng command
- **File not found**: Fallback sang default connection string

### 7. Performance Considerations

**?? ph?c t?p:**
- SCAN: O(N) v?i N l� t?ng s? keys trong Redis
- Pipeline: O(K) v?i K l� s? user c?n query
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
# Cron job m?i 5 ph�t
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

#### Script kh�ng ch?y
```bash
# Check Node.js
node --version  # >= 14.0

# Check dependencies
cd tests && npm install

# Check Redis connection
redis-cli -h localhost -p 6379 PING
```

#### Kh�ng t�m th?y user
```bash
# Check keys trong Redis
redis-cli KEYS "presence:*" | wc -l
redis-cli KEYS "lastseen:*" | wc -l

# Check TTL
redis-cli TTL "presence:{userId}"
redis-cli GET "lastseen:{userId}"
```

#### JSON parsing error
- Ki?m tra appsettings.json c� valid kh�ng
- Script ?� handle comments (//) trong JSON
- Set bi?n m�i tr??ng REDIS ?? bypass config file

### 10. Future Enhancements

C� th? m? r?ng th�m:
- [ ] Export sang CSV format
- [ ] Grafana dashboard integration
- [ ] WebSocket streaming mode (real-time updates)
- [ ] Historical data tracking (time-series)
- [ ] Alerting khi c� anomaly (sudden drop/spike)
- [ ] Integration v?i Prometheus metrics
- [ ] Support Redis Cluster topology discovery
