# StudentGamerHub Realtime Chat Test Plan

## Scope & Architecture References
- **Hub**: `WebAPI/Hubs/ChatHub.cs` exposes `SendDm`, `SendToRoom`, `LoadHistory`, `JoinChannels` with rate limiting and Redis-backed history (`Services/Implementations/ChatHistoryService.cs`).
- **Presence Hub**: `WebAPI/Hubs/PresenceHub.cs` stores heartbeats in Redis with TTL defined in `Services/Presence/PresenceOptions.cs`.
- **Rate Limiting**: Configured via `ChatOptions` (`HistoryMax=200`, `HistoryTtlHours=48`, `RateLimitMaxMessages=30`, `RateLimitWindowSeconds=30`).
- **Redis Keys**: DM -> `chat:dm:{pairMin}_{pairMax}`, Room -> `chat:room:{roomId}`, Presence -> `presence:{userId}`.

## Test Levels Overview

### Smoke Suite (1–2 Clients)
| ID | Objective | Preconditions | Steps | Expected Result | Metrics / Logs |
| --- | --- | --- | --- | --- | --- |
| SMK-1 | Validate DM delivery via SignalR (`SendDm`) | Redis reachable; two authenticated users (A/B) | 1. Connect A & B to `/ws/chat`.<br>2. Invoke `JoinChannels` for `dm:{pair}`.<br>3. A sends single message to B. | Both clients receive identical payload with same `Id` and chronological `SentAt`. No duplicates. | JSON log with round-trip latency sample, ordering check, duplicates check, DM key name. |
| SMK-2 | Verify authenticated connection requirement | Users created; test token revoked/empty | 1. Attempt hub connection without Bearer token.<br>2. Observe connection failure. | Unauthorized connection rejected (HTTP 401 / HubException). | Smoke harness logs `FAIL` with error, recorded in checklist for security gate. |

### Functional Suite (DM / Room / History / Presence / Rate Limit / Security)
| ID | Objective | Preconditions | Steps | Expected Result | Metrics / Logs |
| --- | --- | --- | --- | --- | --- |
| FUN-DM-1 | Validate DM fan-out & ordering under `SendDm` | Users A/B registered; Redis running | Re-use SMK-1 harness but send burst of 5 messages alternating A/B. | Messages delivered in send order; each `ChatMessageDto` has monotonic `SentAt`. | Round-trip latencies (P50/P95), ordering validation, duplicate detection. |
| FUN-RM-1 | Validate room broadcast via `SendToRoom` | Host user joined/created room; N members connected | 1. Host + members join `room:{roomId}`.<br>2. Host sends broadcast.<br>3. Verify all participants receive payload. | 100% participants receive with consistent `RoomId`. | Latency per receiver, broadcast success ratio. |
| FUN-HIST-1 | Enforce `HistoryMax` trim via `LoadHistory` | Redis seeded with >200 entries in channel | 1. Seed 220 messages using Redis Streams List.<br>2. Call `LoadHistory(channel, null, 500)`.<br>3. Request incremental history with `afterId`. | Response capped at 200; `NextAfterId` provided; new messages retrievable via `afterId`. | JSON log: returned count, TTL, XLEN, tail comparison, `NextAfterId` validation. |
| FUN-HIST-2 | Validate TTL expiry (48h) configuration | DM channel seeded; TTL measurement window | 1. Query Redis `TTL` for `chat:dm:*`.<br>2. Confirm TTL ~ 172800 seconds after operations. | TTL within ±5% of 48h. | Redis TTL sample in log + CLI instructions. |
| FUN-RL-1 | Verify per-connection rate limiting (30 msg/30s) | Single connection; DM target prepared | 1. Send 40 `SendDm` invocations within <30s.<br>2. Count accepted vs rejected. | First 30 succeed; subsequent 10 throw `HubException("rate_limited")`; connection remains usable. | JSON log counts, error codes, latency of accepted requests. |
| FUN-PR-1 | Presence heartbeat expiry | Presence hub reachable; Redis running | 1. Connect & invoke `Heartbeat` once.<br>2. Wait `HeartbeatSeconds` (30s).<br>3. After `TtlSeconds+grace`, confirm `presence:{user}` removed. | Presence transitions Online → Away → Offline, TTL cleared. | Presence state table, TTL logs, last seen timestamp. |
| FUN-SEC-1 | Room authorization enforcement | User not a member of specific room | 1. Attempt `SendToRoom` without joining room.<br>2. Expect rejection (HubException). | Unauthorized access denied; log error. | Functional harness logs security failure for manual follow-up. |
| FUN-SEC-2 | Payload validation (empty/oversized text) | Valid connection | 1. Invoke `SendDm` with empty string.<br>2. Invoke with >2000 char string. | Hub returns `HubException` with validation message (from `ChatHistoryService.SanitizeText`). | Error logs stored with case `validation`. |
| FUN-ABUSE-1 | Abuse report REST flow (`POST /api/abuse/report`) | Access token for reporter; message metadata | 1. Capture offending message meta via harness.<br>2. Call REST endpoint with `AbuseReportRequest` payload. | API returns 200 with bug report id; JSON log includes request/response snippet. | Postman & harness logs, ensures compliance. |

### Resilience / Load Suite (Network Faults, Reconnect, Burst Users)
| ID | Objective | Preconditions | Steps | Expected Result | Metrics / Logs |
| --- | --- | --- | --- | --- | --- |
| RES-REC-1 | Auto-reconnect + history recovery | Two clients in DM; hub auto-reconnect configured | 1. Drop client A transport.<br>2. Client B sends message during outage.<br>3. Verify A reconnects automatically.<br>4. Invoke `LoadHistory` to recover offline message. | Reconnect <10s; offline message retrieved once; no duplicates. | Log reconnection latency, recovered message ids. |
| RES-NET-1 | Simulate transient hub downtime | Run harness while temporarily stopping BE (manual). | Manual step to observe client exponential backoff. | Clients should retry per `withAutomaticReconnect` policy; logs show retries. | Documented in checklist (manual). |
| RES-LOAD-1 | Broadcast under 50–200 virtual users | k6 script running `tests/realtime/k6-room-broadcast.js` with configured VUs and RPS. | 1. Acquire access token for load user(s).<br>2. Run `k6 run ...` at 50→200 VUs ramp.<br>3. Measure P50/P95 latency, error rate, dropped frames. | P95 < 350ms; error rate <0.5%; no dropped broadcasts. | k6 summary exported + appended to report. |
| RES-REDIS-1 | Redis eviction behaviour under burst history writes | Use `hub-history.js` seeding multiple channels simultaneously. | Keys remain capped at 200 entries; TTL maintained. | Redis CLI sample verifying `XLEN`, `TTL`. | Logged sample commands. |

## Execution Checklist
| Suite | Script / Action | Status | Notes |
| --- | --- | --- | --- |
| Smoke | `node tests/realtime/hub-smoke.js` | ☐ | Validates DM pipeline, logs round-trip latency. |
| Functional | `node tests/realtime/hub-room.js` | ☐ | Broadcast + membership verification. |
| Functional | `node tests/realtime/hub-history.js` | ☐ | History cap/TTL/pagination. |
| Functional | `node tests/realtime/hub-ratelimit.js` | ☐ | Rate limiter enforcement. |
| Functional | `node tests/realtime/hub-presence.js` | ☐ | Presence TTL lifecycle. |
| Functional | Security checks (unauth/invalid payload) | ☐ | Manual via harness toggles or Postman. |
| Functional | Abuse report API (`tests/realtime/postman_collection.json`) | ☐ | Ensure audit trail creation. |
| Resilience | `node tests/realtime/hub-reconnect.js` | ☐ | Reconnect & recovery. |
| Load | `k6 run tests/realtime/k6-room-broadcast.js` | ☐ | 50–200 VUs broadcast soak. |
| Reporting | `node tests/realtime/make-report.js` | ☐ | Aggregate logs → `REPORT.md`. |

## Metrics & Evidence Collection
- **JSON Logs**: All harness scripts emit JSONL entries (`{timestamp, case, step, result, latencyMs, details}`) saved in `tests/realtime/logs/`.
- **Latency Percentiles**: Captured per-case as P50/P95/P99 via `make-report.js` (ASCII charts).
- **Redis Evidence**: Scripts log TTL (`TTL`, `XLEN`) and keys; manual Redis CLI guidance provided in README.
- **Security Assertions**: Hub exceptions for unauthorized/payload failures logged with `result=FAIL` to highlight enforcement.
- **Rate Limit Metrics**: Accepted vs rejected counts, error codes extracted in `hub-ratelimit.js` and aggregated into `REPORT.md`.
- **Presence States**: `hub-presence.js` logs state transitions and `lastseen` snapshot.

## Acceptance Gates
- Smoke & Functional suites must report **PASS**.
- DM P95 latency < 250 ms, Room broadcast P95 < 350 ms @ 50 VUs.
- k6 error rate < 0.5% during 1-minute steady state.
- Presence TTL expiry within ±10 s of configured `TtlSeconds`.
- History endpoints return ≤200 messages in correct order with accurate `NextAfterId`.

## Redis Verification Commands (Manual Reference)
```bash
redis-cli -u ${REDIS:-redis://localhost:6379}
> KEYS chat:dm:* chat:room:*
> TTL chat:dm:{pairMin}_{pairMax}
> XLEN chat:room:{roomId}
```
_Avoid `FLUSHALL` in shared environments._

## Security Test Notes
- **Unauthenticated Connection**: Attempt SignalR connection without token → expect HTTP 401.
- **Room Membership Enforcement**: Skip `JoinChannels` then invoke `SendToRoom` → expect `HubException("Unauthorized access to DM channel.")` or validation error depending on target.
- **Payload Validation**: Empty or >2000 char text should trigger `ArgumentException` from `ChatHistoryService.SanitizeText` surfaced as HubException.
- **Abuse Report**: Use `POST /api/abuse/report` with `AbuseReportRequest` containing message snapshot; expect 200 and bug report ID.

## Environment Preparation
- Redis connection: `${REDIS}` (defaults to `redis://localhost:6379`).
- Backend base URL auto-detected via `launchSettings.json` (defaults `https://localhost:7227`).
- Hub URLs: Chat `${HUB_URL:-${BASE_URL}/ws/chat}`, Presence `${PRESENCE_HUB_URL:-${BASE_URL}/ws/presence}`.
- Test users A/B auto-provisioned via `/api/auth/user-register` if absent. Override credentials with `USER_A_EMAIL`, `USER_B_EMAIL`, etc.
- Rooms auto-seeded via REST (`/api/communities`, `/api/clubs`, `/api/rooms`). Provide `ROOM_ID` env to reuse existing room.

