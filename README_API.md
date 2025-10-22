# Student Gamer Hub — API Reference (DRAFT)

> **Status:** DRAFT • **Generated at:** 2025-10-15 14:55:17 UTC • **Commit:** `af9f7a8b21ea8a08fbb7ae067813dbc341e724b8`  
> **Total endpoints:** 92

---

## Table of Contents
- [1. Overview](#1-overview)
- [2. Coverage Matrix](#2-coverage-matrix)
- [3. Frontend Integration Guide](#3-frontend-integration-guide)
- [4. Controller Reference](#4-controller-reference)
  - [4.1 AbuseController](#41-abusecontroller)
  - [4.2 AuthController](#42-authcontroller)
  - [4.3 BugReportsController](#43-bugreportscontroller)
  - [4.4 ClubsController](#44-clubscontroller)
  - [4.5 CommunitiesController](#45-communitiescontroller)
  - [4.6 DashboardController](#46-dashboardcontroller)
  - [4.7 EventsController](#47-eventscontroller)
  - [4.8 FriendsController](#48-friendscontroller)
  - [4.9 GoogleAuthController](#49-googleauthcontroller)
  - [4.10 PaymentsController](#410-paymentscontroller)
  - [4.11 QuestsController](#411-questscontroller)
  - [4.12 RegistrationsController](#412-registrationscontroller)
  - [4.13 RolesController](#413-rolescontroller)
  - [4.14 RoomsController](#414-roomscontroller)
  - [4.15 TeammatesController](#415-teammatescontroller)
  - [4.16 UsersController](#416-userscontroller)
- [5. SignalR](#5-signalr)
- [6. Rate limit & Policies](#6-rate-limit--policies)
- [7. Appendix](#7-appendix)

---

## 1. Overview

**Base URL (Development)**  
- `https://localhost:7227` **or** `http://localhost:5277`  
  *(See `WebAPI/Properties/launchSettings.json`)*

**Authentication & CSRF**  
- Access: `Authorization: Bearer <JWT>`  
- Refresh: HttpOnly cookie  
- CSRF: send header with value from CSRF cookie  
  - Cookie/Header names: `AuthCookie.RefreshName`, `AuthCookie.CsrfHeader` (service: `CsrfService`)

**Time zone**  
- All timestamps in DTOs are **UTC** (`*AtUtc` properties).

**Error Envelope** (ASP.NET Core `ProblemDetails`)
```json
{
  "type": "https://httpstatuses.io/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "TODO: detail message",
  "instance": "/api/placeholder",
  "code": "Bad Request",
  "traceId": "00-00000000000000000000000000000000-0000000000000000-00"
}
```

**Pagination**  
- Offset: `page`, `size`  
- Cursor-based: `cursor`, `size`, `sort`, `desc` (varies by endpoint)  
> TODO: fill exact limits from service layer.

**Filtering & Sorting**  
- Per-endpoint rules.  
> TODO: confirm operators/validation from service/validators.

**Rate limit**  
- Token bucket configured in `WebAPI/Extensions/ServiceCollectionExtensions.cs`.  
  See [§6. Rate limit & Policies](#6-rate-limit--policies).

---

## 2. Coverage Matrix

| Controller              | # Endpoints | Status / Notes |
|-------------------------|:-----------:|----------------|
| AbuseController         | 1           | TODO |
| AuthController          | 12          | TODO |
| BugReportsController    | 6           | TODO |
| ClubsController         | 5           | TODO |
| CommunitiesController   | 6           | TODO |
| DashboardController     | 1           | TODO |
| EventsController        | 6           | TODO |
| FriendsController       | 5           | TODO |
| GoogleAuthController    | 1           | TODO |
| PaymentsController      | 5           | TODO |
| QuestsController        | 4           | TODO |
| RegistrationsController | 3           | TODO |
| RolesController         | 12          | TODO |
| RoomsController         | 10          | TODO |
| TeammatesController     | 1           | TODO |
| UsersController         | 14          | TODO |

> See detailed endpoint catalogs in [§4. Controller Reference](#4-controller-reference).

---

## 3. Frontend Integration Guide

**Login**  
`POST /api/Auth/login` with `LoginRequest`.  
- Server returns **access token**; **refresh token** is set in **HttpOnly** cookie.  
- FE must store **CSRF token** from cookie `AuthCookie.CsrfName` (send back as header `AuthCookie.CsrfHeader`).

**Refresh token**  
`POST /api/Auth/refresh`  
- Read refresh token from cookie (optionally support body fallback).  
- Must send CSRF header.

**Revoke / Logout**  
`POST /api/Auth/revoke`  
- Clears refresh cookie. Subject to rate limit.

**HTTP Interceptor (recommended flow)**
1. On **401**, call **refresh** once.  
2. Update `Authorization` header with new access token.  
3. **Retry exactly once**.  
> TODO: add code snippet.

**Postman / Thunder Client**  
- Variables: `{{baseUrl}}`, `{{authToken}}`, `{{csrfToken}}`.  
> TODO: provide collection & test scripts.

**OpenAPI / Scalar**  
- Run application and open `/docs`.  
> TODO: verify `openapi.yaml` generation.

---

## 4. Controller Reference

> **Conventions**  
> - All times in UTC.  
> - If not stated, assume no additional headers beyond Auth/CSRF.  
> - “Code” lines reference file & anchor line (approx.) for quick navigation.

### 4.1 AbuseController

**Overview:** TODO (see `WebAPI/Controllers/AbuseController.cs`).

**Endpoint Catalog**
| Method | Path                | Short | Auth |
|-------:|---------------------|-------|------|
| POST   | `/api/abuse/report` | TODO  | `[Authorize]` |

**Endpoint Details**

<details>
<summary><code>POST /api/abuse/report</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `BugsWrite`  
- **Path params:** —  
- **Query:** —  
- **Body DTO:** `AbuseReportRequest` — `DTOs/Chat/AbuseReportRequest.cs:L6`  
- **Responses:** `200 OK: object (TODO schema)`; `400/401/429/500: ProblemDetails`  
- **Code:** `WebAPI/Controllers/AbuseController.cs:L16`
</details>

---

### 4.2 AuthController

**Overview:** TODO (see `WebAPI/Controllers/AuthController.cs`).

**Endpoint Catalog**
| Method | Path                                   | Short | Auth |
|-------:|----------------------------------------|-------|------|
| POST   | `/api/Auth/user-register`              | TODO  | `AllowAnonymous` |
| POST   | `/api/Auth/login`                      | TODO  | `AllowAnonymous` |
| POST   | `/api/Auth/refresh`                    | TODO  | `AllowAnonymous` |
| POST   | `/api/Auth/revoke`                     | TODO  | `[Authorize]` |
| POST   | `/api/Auth/email:confirm`              | TODO  | `AllowAnonymous` |
| POST   | `/api/Auth/password:send-reset`        | TODO  | `AllowAnonymous` |
| POST   | `/api/Auth/password:reset`             | TODO  | `AllowAnonymous` |
| GET    | `/api/Auth/me`                         | TODO  | `[Authorize]` |
| PUT    | `/api/Auth/me`                         | TODO  | `[Authorize]` |
| POST   | `/api/Auth/me/password:change`         | TODO  | `[Authorize]` |
| POST   | `/api/Auth/me/email:send-confirm`      | TODO  | `[Authorize]` |
| POST   | `/api/Auth/me/email:send-change`       | TODO  | `[Authorize]` |

**Endpoint Details**

<details>
<summary><code>POST /api/Auth/user-register</code> — TODO</summary>

- **Auth:** `AllowAnonymous`  
- **Body DTO:** `RegisterRequest` — `DTOs/Users/Requests/UserRequest.cs:L22`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/AuthController.cs:L6`
</details>

<details>
<summary><code>POST /api/Auth/login</code> — TODO</summary>

- **Auth:** `AllowAnonymous`  
- **Body DTO:** `LoginRequest` — `DTOs/Auth/Requests/AuthRequests.cs:L5`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/AuthController.cs:L43`
</details>

<details>
<summary><code>POST /api/Auth/refresh</code> — TODO</summary>

- **Auth:** `AllowAnonymous`  
- **Body DTO:** `RefreshTokenRequest?` — `DTOs/Auth/Requests/AuthRequests.cs:L13`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/AuthController.cs:L63`
</details>

<details>
<summary><code>POST /api/Auth/revoke</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Body DTO:** `RevokeTokenRequest?` — `DTOs/Auth/Requests/AuthRequests.cs:L18`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/AuthController.cs:L88`
</details>

<details>
<summary><code>POST /api/Auth/email:confirm</code> — TODO</summary>

- **Auth:** `AllowAnonymous`  
- **Body DTO:** `ConfirmEmailRequest` — `DTOs/Users/Requests/UserRequest.cs:L71`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/AuthController.cs:L106`
</details>

<details>
<summary><code>POST /api/Auth/password:send-reset</code> — TODO</summary>

- **Auth:** `AllowAnonymous`  
- **Query:** `callbackBaseUrl: string?`  
- **Body DTO:** `ForgotPasswordRequest` — `DTOs/Users/Requests/UserRequest.cs:L59`  
- **Code:** `WebAPI/Controllers/AuthController.cs:L119`
</details>

<details>
<summary><code>POST /api/Auth/password:reset</code> — TODO</summary>

- **Auth:** `AllowAnonymous`  
- **Body DTO:** `ResetPasswordRequest` — `DTOs/Users/Requests/UserRequest.cs:L63`  
- **Code:** `WebAPI/Controllers/AuthController.cs:L132`
</details>

<details>
<summary><code>GET /api/Auth/me</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/AuthController.cs:L139`
</details>

<details>
<summary><code>PUT /api/Auth/me</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Body DTO:** `UpdateUserSelfRequest` — `DTOs/Users/Requests/UserRequest.cs:L45`  
- **Code:** `WebAPI/Controllers/AuthController.cs:L148`
</details>

<details>
<summary><code>POST /api/Auth/me/password:change</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Body DTO:** `ChangePasswordRequest` — `DTOs/Users/Requests/UserRequest.cs:L54`  
- **Code:** `WebAPI/Controllers/AuthController.cs:L157`
</details>

<details>
<summary><code>POST /api/Auth/me/email:send-confirm</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Query:** `callbackBaseUrl: string (required)`  
- **Code:** `WebAPI/Controllers/AuthController.cs:L166`
</details>

<details>
<summary><code>POST /api/Auth/me/email:send-change</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Query:** `callbackBaseUrl: string (required)`  
- **Body DTO:** `ChangeEmailRequest` — `DTOs/Users/Requests/UserRequest.cs:L76`  
- **Code:** `WebAPI/Controllers/AuthController.cs:L175`
</details>

---

### 4.3 BugReportsController

**Overview:** TODO (see `WebAPI/Controllers/BugReportsController.cs`).

**Endpoint Catalog**
| Method | Path                                  | Short | Auth |
|-------:|---------------------------------------|-------|------|
| POST   | `/api/BugReports`                     | TODO  | `[Authorize]` |
| GET    | `/api/BugReports/{id:guid}`           | TODO  | `[Authorize]` |
| GET    | `/api/BugReports/my`                  | TODO  | `[Authorize]` |
| GET    | `/api/BugReports`                     | TODO  | `[Authorize(Roles = "Admin")]` |
| GET    | `/api/BugReports/status/{status}`     | TODO  | `[Authorize(Roles = "Admin")]` |
| PATCH  | `/api/BugReports/{id:guid}/status`    | TODO  | `[Authorize(Roles = "Admin")]` |

**Endpoint Details**

<details>
<summary><code>POST /api/BugReports</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `BugsWrite`  
- **Body DTO:** `BugReportCreateRequest` — `DTOs/Bugs/BugReportCreateRequest.cs:L3`  
- **Responses:** `201 BugReportDto`; `400 ProblemDetails`  
- **Code:** `WebAPI/Controllers/BugReportsController.cs:L7`
</details>

<details>
<summary><code>GET /api/BugReports/{id:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `ReadsLight`  
- **Path:** `id: Guid`  
- **Responses:** `200 BugReportDto`; `404 ProblemDetails`  
- **Code:** `WebAPI/Controllers/BugReportsController.cs:L48`
</details>

<details>
<summary><code>GET /api/BugReports/my</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `ReadsLight`  
- **Query:** `page?: int`, `size?: int`  
- **Responses:** `200 PagedResult<BugReportDto>`  
- **Code:** `WebAPI/Controllers/BugReportsController.cs:L67`
</details>

<details>
<summary><code>GET /api/BugReports</code> — TODO</summary>

- **Auth:** `[Authorize(Roles="Admin")]`  
- **Rate limit:** `DashboardRead`  
- **Query:** `page?: int`, `size?: int`  
- **Responses:** `200 PagedResult<BugReportDto>`  
- **Code:** `WebAPI/Controllers/BugReportsController.cs:L91`
</details>

<details>
<summary><code>GET /api/BugReports/status/{status}</code> — TODO</summary>

- **Auth:** `[Authorize(Roles="Admin")]`  
- **Rate limit:** `DashboardRead`  
- **Path:** `status: string`  
- **Query:** `page?: int`, `size?: int`  
- **Responses:** `200 PagedResult<BugReportDto>`; `400 ProblemDetails`  
- **Code:** `WebAPI/Controllers/BugReportsController.cs:L113`
</details>

<details>
<summary><code>PATCH /api/BugReports/{id:guid}/status</code> — TODO</summary>

- **Auth:** `[Authorize(Roles="Admin")]`  
- **Rate limit:** `ReadsLight`  
- **Path:** `id: Guid`  
- **Body DTO:** `BugReportStatusPatchRequest` — `DTOs/Bugs/BugReportStatusPatchRequest.cs:L3`  
- **Responses:** `200 BugReportDto`; `400/404 ProblemDetails`  
- **Code:** `WebAPI/Controllers/BugReportsController.cs:L136`
</details>

---

### 4.4 ClubsController

**Overview:** TODO (see `WebAPI/Controllers/ClubsController.cs`).

**Endpoint Catalog**
| Method | Path                                      | Short | Auth |
|-------:|-------------------------------------------|-------|------|
| GET    | `/api/communities/{communityId:guid}/clubs` | TODO | `[Authorize]` |
| POST   | `/api/Clubs`                              | TODO  | `[Authorize]` |
| GET    | `/api/Clubs/{id:guid}`                    | TODO  | `[Authorize]` |
| PUT    | `/api/Clubs/{id:guid}`                    | TODO  | `[Authorize]` |
| DELETE | `/api/Clubs/{id:guid}`                    | TODO  | `[Authorize]` |

**Endpoint Details**

<details>
<summary><code>GET /api/communities/{communityId:guid}/clubs</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `ClubsRead`  
- **Path:** `communityId: Guid`  
- **Query:** `name?: string`, `isPublic?: bool`, `membersFrom?: int`, `membersTo?: int`, `cursor?: string`, `size: int=20`  
- **Responses:** `200 CursorPageResult<ClubBriefDto>`; `400/401/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/ClubsController.cs:L10`
</details>

<details>
<summary><code>POST /api/Clubs</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `ClubsWrite`  
- **Body DTO:** `ClubCreateRequestDto` — `DTOs/Clubs/ClubCreateRequestDto.cs:L7`  
- **Responses:** `201 Guid`; `400/401/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/ClubsController.cs:L94`
</details>

<details>
<summary><code>GET /api/Clubs/{id:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `ClubsRead`  
- **Path:** `id: Guid`  
- **Responses:** `200 ClubDetailDto`; `401/404 ProblemDetails`  
- **Code:** `WebAPI/Controllers/ClubsController.cs:L128`
</details>

<details>
<summary><code>PUT /api/Clubs/{id:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `ClubsWrite`  
- **Path:** `id: Guid`  
- **Body DTO:** `ClubUpdateRequestDto` — `DTOs/Clubs/ClubUpdateRequestDto.cs:L6`  
- **Responses:** `204`; `400/401/403/404 ProblemDetails`  
- **Code:** `WebAPI/Controllers/ClubsController.cs:L145`
</details>

<details>
<summary><code>DELETE /api/Clubs/{id:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `ClubsWrite`  
- **Path:** `id: Guid`  
- **Responses:** `204`; `401/403/404 ProblemDetails`  
- **Code:** `WebAPI/Controllers/ClubsController.cs:L169`
</details>

---

### 4.5 CommunitiesController

**Overview:** TODO (see `WebAPI/Controllers/CommunitiesController.cs`).

**Endpoint Catalog**
| Method | Path                           | Short | Auth |
|-------:|--------------------------------|-------|------|
| POST   | `/api/Communities`             | TODO  | `[Authorize]` |
| GET    | `/api/Communities`             | TODO  | `[Authorize]` |
| GET    | `/api/Communities/{id:guid}`   | TODO  | `[Authorize]` |
| PUT    | `/api/Communities/{id:guid}`   | TODO  | `[Authorize]` |
| DELETE | `/api/Communities/{id:guid}`   | TODO  | `[Authorize]` |
| GET    | `/api/Communities/discover`    | TODO  | `AllowAnonymous` |

**Endpoint Details**

<details>
<summary><code>POST /api/Communities</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `CommunitiesWrite`  
- **Body DTO:** `CommunityCreateRequestDto` — `DTOs/Communities/CommunityCreateRequestDto.cs:L11`  
- **Responses:** `201 object`; `400/401/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/CommunitiesController.cs:L10`
</details>

<details>
<summary><code>GET /api/Communities</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `CommunitiesRead`  
- **Query:** `school?: string`, `gameId?: Guid`, `isPublic?: bool`, `membersFrom?: int`, `membersTo?: int`, `cursor?: string`, `size: int=20`  
- **Responses:** `200 CursorPageResult<CommunityBriefDto>`; `400/401/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/CommunitiesController.cs:L75`
</details>

<details>
<summary><code>GET /api/Communities/{id:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `CommunitiesRead`  
- **Path:** `id: Guid`  
- **Responses:** `200 CommunityDetailDto`; `401/404 ProblemDetails`  
- **Code:** `WebAPI/Controllers/CommunitiesController.cs:L123`
</details>

<details>
<summary><code>PUT /api/Communities/{id:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `CommunitiesWrite`  
- **Path:** `id: Guid`  
- **Body DTO:** `CommunityUpdateRequestDto` — `DTOs/Communities/CommunityUpdateRequestDto.cs:L10`  
- **Responses:** `204`; `400/401/404/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/CommunitiesController.cs:L145`
</details>

<details>
<summary><code>DELETE /api/Communities/{id:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `CommunitiesWrite`  
- **Path:** `id: Guid`  
- **Responses:** `204`; `401/403/404/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/CommunitiesController.cs:L172`
</details>

<details>
<summary><code>GET /api/Communities/discover</code> — TODO</summary>

- **Auth:** `AllowAnonymous`  
- **Rate limit:** `CommunitiesRead`  
- **Query:** `school?: string`, `gameId?: Guid`, `cursor?: string`, `size?: int`  
- **Responses:** `200 PagedResult<CommunityDetailDto>`; `400/429 ProblemDetails`
- **Code:** `WebAPI/Controllers/CommunitiesController.cs:L208`
</details>

---

### 4.6 DashboardController

**Overview:** TODO (see `WebAPI/Controllers/DashboardController.cs`).

**Endpoint Catalog**
| Method | Path                   | Short | Auth |
|-------:|------------------------|-------|------|
| GET    | `/api/Dashboard/today` | TODO  | `[Authorize]` |

**Endpoint Details**

<details>
<summary><code>GET /api/Dashboard/today</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `DashboardRead`  
- **Responses:** `200 DashboardTodayDto`; `401/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/DashboardController.cs:L12`
</details>

---

### 4.7 EventsController

**Overview:** TODO (see `WebAPI/Controllers/EventsController.cs`).

**Endpoint Catalog**
| Method | Path                                   | Short | Auth |
|-------:|----------------------------------------|-------|------|
| POST   | `/api/Events`                          | TODO  | `[Authorize]` |
| POST   | `/api/Events/{eventId:guid}/open`      | TODO  | `[Authorize]` |
| POST   | `/api/Events/{eventId:guid}/cancel`    | TODO  | `[Authorize]` |
| GET    | `/api/Events/{eventId:guid}`           | TODO  | `[Authorize]` |
| GET    | `/api/Events`                          | TODO  | `[Authorize]` |
| GET    | `/api/organizer/events`                | TODO  | `[Authorize]` |

**Endpoint Details**

<details>
<summary><code>POST /api/Events</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `EventsWrite`  
- **Body DTO:** `EventCreateRequestDto` — `DTOs/Events/EventCreateRequestDto.cs:L3`  
- **Responses:** `201 object`; `400/401/403/404/409/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/EventsController.cs:L5`
</details>

<details>
<summary><code>POST /api/Events/{eventId:guid}/open</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `EventsWrite`  
- **Path:** `eventId: Guid`  
- **Responses:** `204`; `403 object`; `400/401/404/409/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/EventsController.cs:L57`
</details>

<details>
<summary><code>POST /api/Events/{eventId:guid}/cancel</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `EventsWrite`  
- **Path:** `eventId: Guid`  
- **Responses:** `204`; `400/401/403/404/409/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/EventsController.cs:L85`
</details>

<details>
<summary><code>GET /api/Events/{eventId:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `ReadsLight`  
- **Path:** `eventId: Guid`  
- **Responses:** `200 EventDetailDto`; `401/403/404/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/EventsController.cs:L107`
</details>

<details>
<summary><code>GET /api/Events</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `ReadsHeavy`  
- **Query:**  
  `statuses?: IEnumerable<EventStatus>`, `communityId?: Guid`, `organizerId?: Guid`,  
  `from?: DateTimeOffset`, `to?: DateTimeOffset`, `search?: string`, `sort?: string`,  
  `page: int=1`, `pageSize: int=PaginationOptions.DefaultPageSize`  
- **Responses:** `200 PagedResponse<EventDetailDto>`; `401/403/404/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/EventsController.cs:L127`
</details>

<details>
<summary><code>GET /api/organizer/events</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `ReadsHeavy`  
- **Query:**  
  `statuses?: IEnumerable<EventStatus>`, `communityId?: Guid`,  
  `from?: DateTimeOffset`, `to?: DateTimeOffset`, `search?: string`, `sort?: string`,  
  `page: int=1`, `pageSize: int=PaginationOptions.DefaultPageSize`  
- **Responses:** `200 PagedResponse<EventDetailDto>`; `401/403/404/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/EventsController.cs:L180`
</details>

---

### 4.8 FriendsController

**Overview:** TODO (see `WebAPI/Controllers/FriendsController.cs`).

**Endpoint Catalog**
| Method | Path                                 | Short | Auth |
|-------:|--------------------------------------|-------|------|
| POST   | `/api/Friends/{userId:guid}/invite`  | TODO  | `[Authorize]` |
| POST   | `/api/Friends/{userId:guid}/accept`  | TODO  | `[Authorize]` |
| POST   | `/api/Friends/{userId:guid}/decline` | TODO  | `[Authorize]` |
| POST   | `/api/Friends/{userId:guid}/cancel`  | TODO  | `[Authorize]` |
| GET    | `/api/Friends`                        | TODO  | `[Authorize]` |

**Endpoint Details**

<details>
<summary><code>POST /api/Friends/{userId:guid}/invite</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `FriendInvite`  
- **Path:** `userId: Guid`  
- **Responses:** `204`; `400/401/403/404/409/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/FriendsController.cs:L8`
</details>

<details>
<summary><code>POST /api/Friends/{userId:guid}/accept</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `FriendAction`  
- **Path:** `userId: Guid`  
- **Responses:** `204`; `400/401/403/404/409/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/FriendsController.cs:L42`
</details>

<details>
<summary><code>POST /api/Friends/{userId:guid}/decline</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `FriendAction`  
- **Path:** `userId: Guid`  
- **Responses:** `204`; `400/401/403/404/409/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/FriendsController.cs:L65`
</details>

<details>
<summary><code>POST /api/Friends/{userId:guid}/cancel</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `FriendAction`  
- **Path:** `userId: Guid`  
- **Responses:** `204`; `400/401/403/404/409/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/FriendsController.cs:L88`
</details>

<details>
<summary><code>GET /api/Friends</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Query:** `filter?: string`, `request: CursorRequest`  
- **Responses:** `200 CursorPageResult<FriendDto>`; `400/401/403/404/409/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/FriendsController.cs:L111`
</details>

---

### 4.9 GoogleAuthController

**Overview:** TODO (see `WebAPI/Controllers/GoogleAuthController.cs`).

**Endpoint Catalog**
| Method | Path                      | Short | Auth |
|-------:|---------------------------|-------|------|
| POST   | `/api/GoogleAuth/login`   | TODO  | `AllowAnonymous` |

**Endpoint Details**

<details>
<summary><code>POST /api/GoogleAuth/login</code> — TODO</summary>

- **Auth:** `AllowAnonymous`  
- **Body DTO:** `GoogleLoginRequest` — `DTOs/Auth/Requests/GoogleLoginRequest.cs:L5`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/GoogleAuthController.cs:L5`
</details>

---

### 4.10 PaymentsController

**Overview:** TODO (see `WebAPI/Controllers/PaymentsController.cs`).

**Endpoint Catalog**
| Method | Path                                             | Short | Auth |
|-------:|--------------------------------------------------|-------|------|
| POST   | `/api/Payments/{intentId:guid}/confirm`         | TODO  | `[Authorize]` |
| GET    | `/api/Payments/{intentId:guid}`                 | TODO  | `[Authorize]` |
| POST   | `/api/Payments/{intentId:guid}/vnpay/checkout`  | TODO  | `[Authorize]` |
| POST   | `/api/Payments/webhooks/vnpay`                  | TODO  | `AllowAnonymous` |
| GET    | `/api/Payments/vnpay/return`                    | TODO  | `AllowAnonymous` |

**Endpoint Details**

<details>
<summary><code>POST /api/Payments/{intentId:guid}/confirm</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `PaymentsWrite`  
- **Path:** `intentId: Guid`  
- **Responses:** `204`; `400/401/403/404/409/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/PaymentsController.cs:L6`
</details>

<details>
<summary><code>GET /api/Payments/{intentId:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `ReadsLight`  
- **Path:** `intentId: Guid`  
- **Responses:** `200 PaymentIntentDto`; `401/403/404/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/PaymentsController.cs:L42`
</details>

<details>
<summary><code>POST /api/Payments/{intentId:guid}/vnpay/checkout</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `PaymentsWrite`  
- **Path:** `intentId: Guid`  
- **Body DTO:** `VnPayCheckoutRequest?` *(definition not found)*  
- **Responses:** `200 string`; `400/401/403/404/409/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/PaymentsController.cs:L62`
</details>

<details>
<summary><code>POST /api/Payments/webhooks/vnpay</code> — TODO</summary>

- **Auth:** `AllowAnonymous`  
- **Rate limit:** `PaymentsWebhook`  
- **Responses:** `200 VnPayWebhookResponse`  
- **Code:** `WebAPI/Controllers/PaymentsController.cs:L87`
</details>

<details>
<summary><code>GET /api/Payments/vnpay/return</code> — TODO</summary>

- **Auth:** `AllowAnonymous`  
- **Responses:** `302`; `400`  
- **Code:** `WebAPI/Controllers/PaymentsController.cs:L113`
</details>

---

### 4.11 QuestsController

**Overview:** TODO (see `WebAPI/Controllers/QuestsController.cs`).

**Endpoint Catalog**
| Method | Path                                    | Short | Auth |
|-------:|-----------------------------------------|-------|------|
| GET    | `/api/Quests/today`                     | TODO  | `[Authorize]` |
| POST   | `/api/Quests/check-in`                  | TODO  | `[Authorize]` |
| POST   | `/api/Quests/join-room/{roomId:guid}`   | TODO  | `[Authorize]` |
| POST   | `/api/Quests/attend-event/{eventId:guid}` | TODO | `[Authorize]` |

**Endpoint Details**

<details>
<summary><code>GET /api/Quests/today</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Responses:** `200 QuestTodayDto`; `401/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/QuestsController.cs:L15`
</details>

<details>
<summary><code>POST /api/Quests/check-in</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Responses:** `204`; `400/401/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/QuestsController.cs:L51`
</details>

<details>
<summary><code>POST /api/Quests/join-room/{roomId:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Path:** `roomId: Guid`  
- **Responses:** `204`; `400/401/404/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/QuestsController.cs:L73`
</details>

<details>
<summary><code>POST /api/Quests/attend-event/{eventId:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Path:** `eventId: Guid`  
- **Responses:** `204`; `400/401/404/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/QuestsController.cs:L96`
</details>

---

### 4.12 RegistrationsController

**Overview:** TODO (see `WebAPI/Controllers/RegistrationsController.cs`).

**Endpoint Catalog**
| Method | Path                                            | Short | Auth |
|-------:|-------------------------------------------------|-------|------|
| POST   | `/api/events/{eventId:guid}/registrations`     | TODO  | `[Authorize]` |
| GET    | `/api/events/{eventId:guid}/registrations`      | TODO  | `[Authorize]` |
| GET    | `/api/me/registrations`                         | TODO  | `[Authorize]` |

**Endpoint Details**

<details>
<summary><code>POST /api/events/{eventId:guid}/registrations</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `RegistrationsWrite`  
- **Path:** `eventId: Guid`  
- **Responses:** `201 object`; `400/401/403/404/409/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/RegistrationsController.cs:L7`
</details>

<details>
<summary><code>GET /api/events/{eventId:guid}/registrations</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `ReadsHeavy`  
- **Path:** `eventId: Guid`  
- **Query:** `statuses?: IEnumerable<EventRegistrationStatus>`, `page: int=1`, `pageSize: int=PaginationOptions.DefaultPageSize`  
- **Responses:** `200 PagedResponse<RegistrationListItemDto>`; `401/403/404/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/RegistrationsController.cs:L56`
</details>

<details>
<summary><code>GET /api/me/registrations</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `ReadsLight`  
- **Query:** `statuses?: IEnumerable<EventRegistrationStatus>`, `page: int=1`, `pageSize: int=PaginationOptions.DefaultPageSize`  
- **Responses:** `200 PagedResponse<MyRegistrationDto>`; `401/403/404/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/RegistrationsController.cs:L89`
</details>

---

### 4.13 RolesController

**Overview:** TODO (see `WebAPI/Controllers/RolesController.cs`).

**Endpoint Catalog**
| Method | Path                                    | Short | Auth |
|-------:|-----------------------------------------|-------|------|
| GET    | `/api/Roles`                            | TODO  | No |
| GET    | `/api/Roles/exists`                     | TODO  | No |
| POST   | `/api/Roles`                            | TODO  | No |
| PUT    | `/api/Roles/{id:guid}`                  | TODO  | No |
| DELETE | `/api/Roles/{id:guid}`                  | TODO  | No |
| POST   | `/api/Roles/{id:guid}/soft-delete`      | TODO  | No |
| POST   | `/api/Roles/{id:guid}/restore`          | TODO  | No |
| POST   | `/api/Roles/batch-create`               | TODO  | No |
| PUT    | `/api/Roles/batch-update`               | TODO  | No |
| DELETE | `/api/Roles/batch-delete`               | TODO  | No |
| POST   | `/api/Roles/batch-soft-delete`          | TODO  | No |
| POST   | `/api/Roles/batch-restore`              | TODO  | No |

**Endpoint Details**

<details>
<summary><code>GET /api/Roles</code> — TODO</summary>

- **Query:** `paging: PageRequest`, `filter: RoleFilter`  
- **Responses:** `200 PagedResult<RoleDto>`  
- **Code:** `WebAPI/Controllers/RolesController.cs:L3`
</details>

<details>
<summary><code>GET /api/Roles/exists</code> — TODO</summary>

- **Query:** `name: string (required)`, `excludeId?: Guid`  
- **Responses:** `200 bool`  
- **Code:** `WebAPI/Controllers/RolesController.cs:L35`
</details>

<details>
<summary><code>POST /api/Roles</code> — TODO</summary>

- **Body DTO:** `CreateRoleRequest` — `DTOs/Roles/Requests/RoleRequests.cs:L3`  
- **Responses:** `201 RoleDto`; `400/409`  
- **Code:** `WebAPI/Controllers/RolesController.cs:L46`
</details>

<details>
<summary><code>PUT /api/Roles/{id:guid}</code> — TODO</summary>

- **Path:** `id: Guid`  
- **Body DTO:** `UpdateRoleRequest` — `DTOs/Roles/Requests/RoleRequests.cs:L8`  
- **Responses:** `200 RoleDto`; `400/404/409`  
- **Code:** `WebAPI/Controllers/RolesController.cs:L62`
</details>

<details>
<summary><code>DELETE /api/Roles/{id:guid}</code> — TODO</summary>

- **Path:** `id: Guid`  
- **Responses:** `204`; `404`  
- **Code:** `WebAPI/Controllers/RolesController.cs:L74`
</details>

<details>
<summary><code>POST /api/Roles/{id:guid}/soft-delete</code> — TODO</summary>

- **Path:** `id: Guid`  
- **Responses:** `204`; `404`  
- **Code:** `WebAPI/Controllers/RolesController.cs:L84`
</details>

<details>
<summary><code>POST /api/Roles/{id:guid}/restore</code> — TODO</summary>

- **Path:** `id: Guid`  
- **Responses:** `204`; `404`  
- **Code:** `WebAPI/Controllers/RolesController.cs:L94`
</details>

<details>
<summary><code>POST /api/Roles/batch-create</code> — TODO</summary>

- **Body:** `IEnumerable<CreateRoleRequest>` *(definition not found)*  
- **Responses:** `200 BatchResult<Guid, RoleDto>`; `400/409`  
- **Code:** `WebAPI/Controllers/RolesController.cs:L106`
</details>

<details>
<summary><code>PUT /api/Roles/batch-update</code> — TODO</summary>

- **Body:** *(definition not found)*  
- **Responses:** `200 BatchResult<Guid, RoleDto>`; `400/409`  
- **Code:** `WebAPI/Controllers/RolesController.cs:L117`
</details>

<details>
<summary><code>DELETE /api/Roles/batch-delete</code> — TODO</summary>

- **Body:** `IEnumerable<Guid>`  
- **Responses:** `200 BatchOutcome<Guid>`  
- **Code:** `WebAPI/Controllers/RolesController.cs:L128`
</details>

<details>
<summary><code>POST /api/Roles/batch-soft-delete</code> — TODO</summary>

- **Body:** `IEnumerable<Guid>`  
- **Responses:** `200 BatchOutcome<Guid>`  
- **Code:** `WebAPI/Controllers/RolesController.cs:L137`
</details>

<details>
<summary><code>POST /api/Roles/batch-restore</code> — TODO</summary>

- **Body:** `IEnumerable<Guid>`  
- **Responses:** `200 BatchOutcome<Guid>`  
- **Code:** `WebAPI/Controllers/RolesController.cs:L146`
</details>

---

### 4.14 RoomsController

**Overview:** TODO (see `WebAPI/Controllers/RoomsController.cs`).

**Endpoint Catalog**
| Method | Path                                         | Short | Auth |
|-------:|----------------------------------------------|-------|------|
| GET    | `/api/Rooms/{id:guid}`                       | TODO  | `[Authorize]` |
| GET    | `/api/Rooms/{id:guid}/members`               | TODO  | `[Authorize]` |
| POST   | `/api/Rooms`                                 | TODO  | `[Authorize]` |
| POST   | `/api/Rooms/{id:guid}/join`                  | TODO  | `[Authorize]` |
| POST   | `/api/Rooms/{id:guid}/approve/{userId:guid}` | TODO  | `[Authorize]` |
| POST   | `/api/Rooms/{id:guid}/leave`                 | TODO  | `[Authorize]` |
| POST   | `/api/Rooms/{id:guid}/kickban/{userId:guid}` | TODO  | `[Authorize]` |
| PUT    | `/api/Rooms/{id:guid}`                       | TODO  | `[Authorize]` |
| POST   | `/api/Rooms/{id:guid}/transfer-ownership/{newOwnerId:guid}` | TODO | `[Authorize]` |
| DELETE | `/api/Rooms/{id:guid}`                       | TODO  | `[Authorize]` |

**Endpoint Details**

<details>
<summary><code>GET /api/Rooms/{id:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `RoomsRead`  
- **Path:** `id: Guid`  
- **Responses:** `200 RoomDetailDto`; `404/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/RoomsController.cs:L10`
</details>

<details>
<summary><code>GET /api/Rooms/{id:guid}/members</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `RoomsRead`  
- **Path:** `id: Guid`  
- **Query:** `skip: int=0`, `take: int=20`  
- **Responses:** `200 IReadOnlyList<RoomMemberBriefDto>`; `400/404/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/RoomsController.cs:L54`
</details>

<details>
<summary><code>POST /api/Rooms</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `RoomsCreate`  
- **Body DTO:** `RoomCreateRequestDto` — `DTOs/Rooms/RoomCreateRequestDto.cs:L6`  
- **Responses:** `201 Guid`; `400/401/404/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/RoomsController.cs:L83`
</details>

<details>
<summary><code>POST /api/Rooms/{id:guid}/join</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `RoomsWrite`  
- **Path:** `id: Guid`  
- **Body DTO:** `RoomJoinRequestDto` — `DTOs/Rooms/RoomJoinRequestDto.cs:L6`  
- **Responses:** `204`; `400/401/403/404/409/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/RoomsController.cs:L135`
</details>

<details>
<summary><code>POST /api/Rooms/{id:guid}/approve/{userId:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `RoomsWrite`  
- **Path:** `id: Guid`, `userId: Guid`  
- **Responses:** `204`; `401/403/404/409/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/RoomsController.cs:L177`
</details>

<details>
<summary><code>POST /api/Rooms/{id:guid}/leave</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `RoomsWrite`  
- **Path:** `id: Guid`  
- **Responses:** `204`; `401/403/404/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/RoomsController.cs:L217`
</details>

<details>
<summary><code>POST /api/Rooms/{id:guid}/kickban/{userId:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `RoomsWrite`  
- **Path:** `id: Guid`, `userId: Guid`  
- **Query:** `ban: bool=false`  
- **Responses:** `204`; `401/403/404/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/RoomsController.cs:L256`
</details>

<details>
<summary><code>PUT /api/Rooms/{id:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `RoomsWrite`  
- **Path:** `id: Guid`  
- **Body DTO:** `RoomUpdateRequestDto` — `DTOs/Rooms/RoomUpdateRequestDto.cs:L6`  
- **Responses:** `204`; `400/401/403/404/409/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/RoomsController.cs:L298`
</details>

<details>
<summary><code>POST /api/Rooms/{id:guid}/transfer-ownership/{newOwnerId:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `RoomsWrite`  
- **Path:** `id: Guid`, `newOwnerId: Guid`  
- **Responses:** `204`; `401/403/404/409/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/RoomsController.cs:L334`
</details>

<details>
<summary><code>DELETE /api/Rooms/{id:guid}</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `RoomsArchive`  
- **Path:** `id: Guid`  
- **Responses:** `204`; `401/403/404/429 ProblemDetails`  
- **Code:** `WebAPI/Controllers/RoomsController.cs:L367`
</details>

---

### 4.15 TeammatesController

**Overview:** TODO (see `WebAPI/Controllers/TeammatesController.cs`).

**Endpoint Catalog**
| Method | Path                | Short | Auth |
|-------:|---------------------|-------|------|
| GET    | `/api/Teammates`    | TODO  | `[Authorize]` |

**Endpoint Details**

<details>
<summary><code>GET /api/Teammates</code> — TODO</summary>

- **Auth:** `[Authorize]`  
- **Rate limit:** `TeammatesRead`  
- **Query:** `gameId?: Guid`, `university?: string`, `skill?: GameSkillLevel`, `onlineOnly: bool=false`, `cursor?: string`, `size: int=20`  
- **Responses:** `200 CursorPageResult<TeammateDto>`; `400/401/429/500 ProblemDetails`  
- **Code:** `WebAPI/Controllers/TeammatesController.cs:L12`
</details>

---

### 4.16 UsersController

**Overview:** TODO (see `WebAPI/Controllers/UsersController.cs`).

**Endpoint Catalog**
| Method | Path                                         | Short | Auth |
|-------:|----------------------------------------------|-------|------|
| GET    | `/api/Users`                                 | TODO  | `[Authorize(Roles = "Admin")]` |
| GET    | `/api/Users/{id:guid}`                       | TODO  | `[Authorize(Roles = "Admin")]` |
| POST   | `/api/Users`                                 | TODO  | `[Authorize(Roles = "Admin")]` |
| PUT    | `/api/Users/{id:guid}`                       | TODO  | `[Authorize(Roles = "Admin")]` |
| PATCH  | `/api/Users/{id:guid}/lockout`               | TODO  | `[Authorize(Roles = "Admin")]` |
| POST   | `/api/Users/{id:guid}/roles:replace`         | TODO  | `[Authorize(Roles = "Admin")]` |
| POST   | `/api/Users/{id:guid}/roles:modify`          | TODO  | `[Authorize(Roles = "Admin")]` |
| POST   | `/api/Users/{id:guid}/password:change`       | TODO  | `[Authorize(Roles = "Admin")]` |
| POST   | `/api/Users/password:send-reset`             | TODO  | `AllowAnonymous` |
| POST   | `/api/Users/password:reset`                  | TODO  | `AllowAnonymous` |
| POST   | `/api/Users/{id:guid}/email:send-confirm`    | TODO  | `[Authorize(Roles = "Admin")]` |
| POST   | `/api/Users/email:confirm`                   | TODO  | `AllowAnonymous` |
| POST   | `/api/Users/{id:guid}/email:send-change`     | TODO  | `[Authorize(Roles = "Admin")]` |
| POST   | `/api/Users/email:confirm-change`            | TODO  | `AllowAnonymous` |

**Endpoint Details**

<details>
<summary><code>GET /api/Users</code> — TODO</summary>

- **Auth:** `[Authorize(Roles="Admin")]`  
- **Query:** `filter: UserFilter`, `page: PageRequest`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/UsersController.cs:L5`
</details>

<details>
<summary><code>GET /api/Users/{id:guid}</code> — TODO</summary>

- **Auth:** `[Authorize(Roles="Admin")]`  
- **Path:** `id: Guid`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/UsersController.cs:L23`
</details>

<details>
<summary><code>POST /api/Users</code> — TODO</summary>

- **Auth:** `[Authorize(Roles="Admin")]`  
- **Body DTO:** `CreateUserAdminRequest` — `DTOs/Users/Requests/UserRequest.cs:L6`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/UsersController.cs:L32`
</details>

<details>
<summary><code>PUT /api/Users/{id:guid}</code> — TODO</summary>

- **Auth:** `[Authorize(Roles="Admin")]`  
- **Path:** `id: Guid`  
- **Body DTO:** `UpdateUserRequest` — `DTOs/Users/Requests/UserRequest.cs:L32`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/UsersController.cs:L41`
</details>

<details>
<summary><code>PATCH /api/Users/{id:guid}/lockout</code> — TODO</summary>

- **Auth:** `[Authorize(Roles="Admin")]`  
- **Path:** `id: Guid`  
- **Body DTO:** `SetLockoutRequest` — `DTOs/Users/Requests/UserRequest.cs:L69`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/UsersController.cs:L50`
</details>

<details>
<summary><code>POST /api/Users/{id:guid}/roles:replace</code> — TODO</summary>

- **Auth:** `[Authorize(Roles="Admin")]`  
- **Path:** `id: Guid`  
- **Body DTO:** `ReplaceRolesRequest` — `DTOs/Users/Requests/UserRequest.cs:L90`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/UsersController.cs:L59`
</details>

<details>
<summary><code>POST /api/Users/{id:guid}/roles:modify</code> — TODO</summary>

- **Auth:** `[Authorize(Roles="Admin")]`  
- **Path:** `id: Guid`  
- **Body DTO:** `ModifyRolesRequest` — `DTOs/Users/Requests/UserRequest.cs:L87`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/UsersController.cs:L67`
</details>

<details>
<summary><code>POST /api/Users/{id:guid}/password:change</code> — TODO</summary>

- **Auth:** `[Authorize(Roles="Admin")]`  
- **Path:** `id: Guid`  
- **Body DTO:** `ChangePasswordRequest` — `DTOs/Users/Requests/UserRequest.cs:L54`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/UsersController.cs:L76`
</details>

<details>
<summary><code>POST /api/Users/password:send-reset</code> — TODO</summary>

- **Auth:** `AllowAnonymous`  
- **Query:** `callbackBaseUrl: string (required)`  
- **Body DTO:** `ForgotPasswordRequest` — `DTOs/Users/Requests/UserRequest.cs:L59`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/UsersController.cs:L84`
</details>

<details>
<summary><code>POST /api/Users/password:reset</code> — TODO</summary>

- **Auth:** `AllowAnonymous`  
- **Body DTO:** `ResetPasswordRequest` — `DTOs/Users/Requests/UserRequest.cs:L63`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/UsersController.cs:L93`
</details>

<details>
<summary><code>POST /api/Users/{id:guid}/email:send-confirm</code> — TODO</summary>

- **Auth:** `[Authorize(Roles="Admin")]`  
- **Path:** `id: Guid`  
- **Query:** `callbackBaseUrl: string (required)`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/UsersController.cs:L103`
</details>

<details>
<summary><code>POST /api/Users/email:confirm</code> — TODO</summary>

- **Auth:** `AllowAnonymous`  
- **Body DTO:** `ConfirmEmailRequest` — `DTOs/Users/Requests/UserRequest.cs:L71`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/UsersController.cs:L111`
</details>

<details>
<summary><code>POST /api/Users/{id:guid}/email:send-change</code> — TODO</summary>

- **Auth:** `[Authorize(Roles="Admin")]`  
- **Path:** `id: Guid`  
- **Query:** `callbackBaseUrl: string (required)`  
- **Body DTO:** `ChangeEmailRequest` — `DTOs/Users/Requests/UserRequest.cs:L76`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/UsersController.cs:L121`
</details>

<details>
<summary><code>POST /api/Users/email:confirm-change</code> — TODO</summary>

- **Auth:** `AllowAnonymous`  
- **Body DTO:** `ConfirmChangeEmailRequest` — `DTOs/Users/Requests/UserRequest.cs:L81`  
- **Responses:** `200/4xx/5xx (TODO)`  
- **Code:** `WebAPI/Controllers/UsersController.cs:L129`
</details>

---

## 5. SignalR

> TODO: Inspect `WebAPI/Hubs` (PresenceHub, ChatHub) and document:
> - Hub URLs
> - Methods & payload contracts
> - Presence semantics
> - Error & reconnect policy

---

## 6. Rate limit & Policies

| Policy             | Limit                 | Applies To |
|--------------------|-----------------------|------------|
| EventsWrite        | 60 req/min/user       | TODO: list Events write endpoints |
| RegistrationsWrite | 60 req/min/user       | TODO |
| PaymentsWrite      | 120 req/min/user      | TODO |
| PaymentsWebhook    | 300 req/min/IP        | TODO |
| ReadsHeavy         | 300 req/min/user      | TODO |
| ReadsLight         | 600 req/min/user      | TODO |
| FriendInvite       | 20 req/day/user       | TODO (Friends invites) |
| FriendAction       | 60 req/min/user       | TODO |
| DashboardRead      | 120 req/min/user      | TODO |
| TeammatesRead      | 120 req/min/user      | `GET /api/Teammates` |
| RoomsCreate        | 10 req/day/user       | TODO |
| RoomsRead          | 120 req/min/user      | TODO |
| RoomsWrite         | 30 req/min/user       | TODO |
| RoomsArchive       | 10 req/day/user       | TODO |
| CommunitiesRead    | 120 req/min/user      | TODO |
| CommunitiesWrite   | 10 req/day/user       | TODO |
| ClubsRead          | 120 req/min/user      | ClubsController reads |
| ClubsWrite         | 10 req/day/user       | ClubsController writes |
| BugsWrite          | 20 req/day/user       | Abuse/BugReports POST |

---

## 7. Appendix

- **Enums**: TODO (e.g., `Gender`, etc.)  
- **Default paging** for `PageRequest` / `CursorRequest`: TODO  
- **Error codes mapping** (`Error.Codes`): TODO

---

### Notes
- Keep this README in the root of the WebAPI repo for quick discovery on GitHub.  
- When ready, add a CI step to export OpenAPI (`openapi.yaml`) and link it here.
