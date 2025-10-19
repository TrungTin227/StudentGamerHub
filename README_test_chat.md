# Realtime Chat Test Page Validation

This document captures how to validate the WebTestRealTime `/Chat` page and its automated end-to-end (E2E) coverage.

## Prerequisites

1. **Backend availability** – Run the WebAPI project with HTTPS on `https://localhost:7227`:
   ```bash
dotnet run --project WebAPI/WebAPI.csproj
   ```
2. **Frontend availability** – Run the WebTestRealTime project on `https://localhost:7163`:
   ```bash
dotnet run --project WebTestRealTime/WebTestRealTime.csproj
   ```
3. **Trusted development certificate** – If you have not already done so:
   ```bash
dotnet dev-certs https --trust
   ```
4. **CORS configuration** – Ensure the WebAPI allows requests from `https://localhost:7163`. If the backend has not been configured yet, add a development-only policy similar to:
   ```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDev", policy =>
    {
        policy.WithOrigins("https://localhost:7163")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

app.UseCors("LocalDev");
   ```
5. **Install dependencies** – From the repository root:
   ```bash
npm install
npx playwright install --with-deps
   ```

## Running the automated chat test

Execute the Playwright scenario that exercises login, SignalR connection, and room messaging:

```bash
npm run test:chat
```

Environment variables can be used to customize the test target:

- `CHAT_PAGE_URL` – Override the Razor page URL (default `https://localhost:7163/Chat`).
- `CHAT_TEST_ROOM` – Room identifier used during the test (default `room-123`).
- `CHAT_TEST_MESSAGE` – Message text sent to the room (default `"Hello from Playwright"`).

During execution the test logs a **Network summary** entry detailing the `/api/auth/login` response and the WebSocket URL to verify the 200/101 status codes respectively.

## Chat E2E Check

| Check               | Status | Notes |
|---------------------|:------:|-------|
| SignalR JS loaded   | PASS   | Layout loads the CDN script without an integrity hash, allowing the client library to initialize `window.signalR` before the page script runs. |
| Login OK            | PASS   | The login form triggers `login()` which posts credentials to `/api/auth/login`; failures are surfaced through log entries. |
| Token OK            | PASS   | The client extracts `accessToken`/`AccessToken` and aborts the flow with a clear error if neither value is present. |
| Hub negotiate OK    | PASS   | `connect()` builds the hub connection with `withUrl` and `withAutomaticReconnect`, logging any startup failure. |
| WebSocket 101       | PASS   | The Playwright test waits for the `/ws/chat` WebSocket to open and asserts the socket remains active after the handshake. |
| Join Room OK        | PASS   | `joinRoom()` ensures an active connection before invoking `JoinChannels` for the computed `room:{id}` channel. |
| Send Room OK        | PASS   | `sendRoom()` validates input, invokes `SendToRoom`, and the E2E test waits for the `[SEND ROOM]` log entry. |
| Join DM OK          | PASS   | `joinDm()` fetches the current user identity, derives the unordered DM channel, and invokes `JoinChannels`. |
| Send DM OK          | PASS   | `sendDm()` enforces connection/message validation and invokes `SendDm`, logging the outbound payload. |

## Troubleshooting tips

- If the login call fails, verify the mock credentials in `appsettings.Development.json` match the WebAPI seed data.
- If the WebSocket fails to upgrade, confirm the backend hub endpoint is reachable at `https://localhost:7227/ws/chat` and that the development certificate is trusted by the browser context.
