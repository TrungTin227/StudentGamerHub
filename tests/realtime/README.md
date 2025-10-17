# Realtime Chat QA Harness

This directory contains automated smoke/functional/resilience/load checks for the StudentGamerHub realtime chat stack (`WebAPI/Hubs/ChatHub.cs`, `Services/Implementations/ChatHistoryService.cs`, `Services/Presence/PresenceService.cs`).

## Prerequisites
1. Redis running locally (`redis://localhost:6379`) or provide `REDIS` env variable.
2. StudentGamerHub backend running in Development (`dotnet run --project WebAPI`). Default URLs come from `WebAPI/Properties/launchSettings.json` (`https://localhost:7227`).
3. Node.js ≥ 18.
4. Install dependencies once:
   ```bash
   npm install @microsoft/signalr cross-fetch ws ioredis strip-json-comments
   # Optional tooling
   npm install --save-dev k6
   ```
5. Export any overrides as needed:
   ```bash
   export BASE_URL=https://localhost:7227
   export HUB_URL=https://localhost:7227/ws/chat
   export PRESENCE_HUB_URL=https://localhost:7227/ws/presence
   export REDIS=redis://localhost:6379
   export USER_A_EMAIL=realtime-a@studentgamerhub.local
   export USER_B_EMAIL=realtime-b@studentgamerhub.local
   export USER_A_PASSWORD=Password123!
   export USER_B_PASSWORD=Password123!
   export ROOM_ID=<existing-room-guid>   # optional; harness will auto-create if missing
   ```

## Running the Suites
| Scenario | Command |
| --- | --- |
| Smoke DM check | `node tests/realtime/hub-smoke.js` |
| Room broadcast | `node tests/realtime/hub-room.js` |
| History retention | `node tests/realtime/hub-history.js` |
| Rate limiting | `node tests/realtime/hub-ratelimit.js` |
| Presence TTL | `node tests/realtime/hub-presence.js` |
| Reconnect recovery | `node tests/realtime/hub-reconnect.js` |
| k6 broadcast load | `k6 run tests/realtime/k6-room-broadcast.js` |
| Aggregate report | `node tests/realtime/make-report.js` |

All scripts emit JSONL logs in `tests/realtime/logs/` with the structure `{timestamp, case, step, result, latencyMs, details}`.

## Test Users & Rooms
- Harness auto-registers users A/B via `POST /api/auth/user-register` if they do not exist, then logs in via `POST /api/auth/login` and caches the `accessToken` for SignalR connections.
- Room scenarios call:
  1. `GET /api/communities?size=1` (fallback to `POST /api/communities`).
  2. `POST /api/clubs` to create a club.
  3. `POST /api/rooms` to create an open room.
  4. `POST /api/rooms/{id}/join` for member enrollment.

Provide `ROOM_ID` to reuse an existing room and skip provisioning.

For the k6 scenario, export a fresh access token (valid for ~2 hours):
```bash
ACCESS_TOKEN=$(curl -sk -X POST "$BASE_URL/api/auth/login" \
  -H 'Content-Type: application/json' \
  -d "{\"userNameOrEmail\":\"$USER_A_EMAIL\",\"password\":\"$USER_A_PASSWORD\"}" | jq -r '.accessToken')
export ACCESS_TOKEN
```

## Environment Variables
| Name | Default | Description |
| --- | --- | --- |
| `BASE_URL` | Auto-read from launch settings | REST API base URL |
| `HUB_URL` | `${BASE_URL}/ws/chat` | Chat hub URL |
| `PRESENCE_HUB_URL` | `${BASE_URL}/ws/presence` | Presence hub URL |
| `REDIS` | `redis://localhost:6379` | Redis connection string |
| `USER_A_*`, `USER_B_*` | Predefined test users | Override credentials/metadata |
| `ROOM_ID` | _null_ | Existing room to reuse |
| `RL_TOTAL` | `40` | Total messages for rate limit harness |
| `ROOM_CLONES` | `2` | Additional member connections in room broadcast |
| `MESSAGE_INTERVAL_MS` | `1000` (k6) | Frequency of load-test sends |
| `VUS`, `HOLD_DURATION` | `50`, `1m` | k6 load configuration |
| `ACCESS_TOKEN` | — | Required by k6 load test |

## Redis Verification
Use `redis-cli` to inspect keys:
```bash
redis-cli -u ${REDIS:-redis://localhost:6379}
> KEYS chat:dm:* chat:room:*
> TTL chat:dm:{pairMin}_{pairMax}
> XLEN chat:room:{roomId}
> TTL presence:{userId}
```
_Important: do **not** call `FLUSHALL` on shared environments._

## Report Generation
Run `node tests/realtime/make-report.js` after executing tests. The script consumes all JSON logs and updates `tests/realtime/REPORT.md` with:
- Pass/fail summary per case.
- Latency P50/P95/P99 and ASCII histograms.
- Redis TTL samples.
- Presence timeline tables.
- Fix suggestions for failing cases.

## Troubleshooting
- **401 Unauthorized**: Ensure the backend is running with HTTPS and the login credentials are valid. Override via `USER_A_EMAIL`/`USER_A_PASSWORD`.
- **TLS issues**: Node may need `NODE_TLS_REJECT_UNAUTHORIZED=0` for self-signed dev certs.
- **Redis connection refused**: Update `REDIS` env to point at accessible instance.
- **k6 handshake failures**: Verify `ACCESS_TOKEN` is valid and the user is approved in the target room (`POST /api/rooms/{id}/join`).

## Related Source References
- Chat Hub implementation: `WebAPI/Hubs/ChatHub.cs`
- History service: `Services/Implementations/ChatHistoryService.cs`
- Chat options: `Services/Configuration/ChatOptions.cs`
- Presence hub: `WebAPI/Hubs/PresenceHub.cs`
- Presence options: `Services/Presence/PresenceOptions.cs`
- Abuse report API: `WebAPI/Controllers/AbuseController.cs`
