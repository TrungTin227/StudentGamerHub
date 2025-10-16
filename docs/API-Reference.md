# Student Gamer Hub API Reference (Draft)

## 1. Tổng quan
- **Base URL (Development):** `https://localhost:7227` hoặc `http://localhost:5277` (xem `WebAPI/Properties/launchSettings.json`).
- **Authentication:** Bearer JWT (header `Authorization: Bearer <token>`), refresh token lưu ở cookie HttpOnly + CSRF header (`AuthCookie.RefreshName`, `AuthCookie.CsrfHeader`, `CsrfService`).
- **Time zone:** Tất cả timestamp trong DTO mặc định dùng UTC (`*AtUtc`).
- **Error envelope:** ASP.NET Core `ProblemDetails`. Ví dụ JSON tham khảo từ `WebAPI/Extensions/FriendsExamplesDocumentTransformer.cs`.
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
- **Pagination:** Offset (`page`, `size`) hoặc cursor (`cursor`, `size`, `sort`, `desc`) tùy endpoint. TODO: điền giới hạn chi tiết từ service layer.
- **Filtering & sorting:** Theo từng endpoint; xem bảng filter ở phần 4.2.2 trở đi. TODO: xác nhận toán tử / validation từ service/validator.
- **Rate limit:** Cấu hình token bucket trong `WebAPI/Extensions/ServiceCollectionExtensions.cs`. Chi tiết tại mục 6.

## 2. Ma trận Bao phủ (Coverage Matrix)
| Controller | Số endpoint | Endpoint (method + path) | Trạng thái |
| --- | --- | --- | --- |
| AbuseController | 1 | POST /api/abuse/report | TODO |
| AuthController | 12 | POST /api/Auth/user-register<br>POST /api/Auth/login<br>POST /api/Auth/refresh<br>POST /api/Auth/revoke<br>POST /api/Auth/email:confirm<br>POST /api/Auth/password:send-reset<br>POST /api/Auth/password:reset<br>GET /api/Auth/me<br>PUT /api/Auth/me<br>POST /api/Auth/me/password:change<br>POST /api/Auth/me/email:send-confirm<br>POST /api/Auth/me/email:send-change | TODO |
| BugReportsController | 6 | POST /api/BugReports<br>GET /api/BugReports/{id:guid}<br>GET /api/BugReports/my<br>GET /api/BugReports<br>GET /api/BugReports/status/{status}<br>PATCH /api/BugReports/{id:guid}/status | TODO |
| ClubsController | 5 | GET /api/communities/{communityId:guid}/clubs<br>POST /api/Clubs<br>GET /api/Clubs/{id:guid}<br>PATCH /api/Clubs/{id:guid}<br>DELETE /api/Clubs/{id:guid} | TODO |
| CommunitiesController | 6 | POST /api/Communities<br>GET /api/Communities<br>GET /api/Communities/{id:guid}<br>PATCH /api/Communities/{id:guid}<br>DELETE /api/Communities/{id:guid}<br>GET /api/Communities/discover | TODO |
| DashboardController | 1 | GET /api/Dashboard/today | TODO |
| EventsController | 6 | POST /api/Events<br>POST /api/Events/{eventId:guid}/open<br>POST /api/Events/{eventId:guid}/cancel<br>GET /api/Events/{eventId:guid}<br>GET /api/Events<br>GET /api/organizer/events | TODO |
| FriendsController | 5 | POST /api/Friends/{userId:guid}/invite<br>POST /api/Friends/{userId:guid}/accept<br>POST /api/Friends/{userId:guid}/decline<br>POST /api/Friends/{userId:guid}/cancel<br>GET /api/Friends | TODO |
| GoogleAuthController | 1 | POST /api/GoogleAuth/login | TODO |
| PaymentsController | 5 | POST /api/Payments/{intentId:guid}/confirm<br>GET /api/Payments/{intentId:guid}<br>POST /api/Payments/{intentId:guid}/vnpay/checkout<br>POST /api/Payments/webhooks/vnpay<br>GET /api/Payments/vnpay/return | TODO |
| QuestsController | 4 | GET /api/Quests/today<br>POST /api/Quests/check-in<br>POST /api/Quests/join-room/{roomId:guid}<br>POST /api/Quests/attend-event/{eventId:guid} | TODO |
| RegistrationsController | 3 | POST /api/events/{eventId:guid}/registrations<br>GET /api/events/{eventId:guid}/registrations<br>GET /api/me/registrations | TODO |
| RolesController | 12 | GET /api/Roles<br>GET /api/Roles/exists<br>POST /api/Roles<br>PUT /api/Roles/{id:guid}<br>DELETE /api/Roles/{id:guid}<br>POST /api/Roles/{id:guid}/soft-delete<br>POST /api/Roles/{id:guid}/restore<br>POST /api/Roles/batch-create<br>PUT /api/Roles/batch-update<br>DELETE /api/Roles/batch-delete<br>POST /api/Roles/batch-soft-delete<br>POST /api/Roles/batch-restore | TODO |
| RoomsController | 10 | GET /api/Rooms/{id:guid}<br>GET /api/Rooms/{id:guid}/members<br>POST /api/Rooms<br>POST /api/Rooms/{id:guid}/join<br>POST /api/Rooms/{id:guid}/approve/{userId:guid}<br>POST /api/Rooms/{id:guid}/leave<br>POST /api/Rooms/{id:guid}/kickban/{userId:guid}<br>PATCH /api/Rooms/{id:guid}<br>POST /api/Rooms/{id:guid}/transfer-ownership/{newOwnerId:guid}<br>DELETE /api/Rooms/{id:guid} | TODO |
| TeammatesController | 1 | GET /api/Teammates | TODO |
| UsersController | 14 | GET /api/Users<br>GET /api/Users/{id:guid}<br>POST /api/Users<br>PUT /api/Users/{id:guid}<br>PATCH /api/Users/{id:guid}/lockout<br>POST /api/Users/{id:guid}/roles:replace<br>POST /api/Users/{id:guid}/roles:modify<br>POST /api/Users/{id:guid}/password:change<br>POST /api/Users/password:send-reset<br>POST /api/Users/password:reset<br>POST /api/Users/{id:guid}/email:send-confirm<br>POST /api/Users/email:confirm<br>POST /api/Users/{id:guid}/email:send-change<br>POST /api/Users/email:confirm-change | TODO |

## 3. Hướng dẫn FE tích hợp (global)
- Đăng nhập: `POST /api/Auth/login` với `LoginRequest` → trả về access token, refresh token set cookie. Lưu CSRF token từ cookie `AuthCookie.CsrfName`.
- Refresh: `POST /api/Auth/refresh` gửi refresh token (header cookie + body dự phòng) + header CSRF `AuthCookie.CsrfHeader`.
- Revoke/logout: `POST /api/Auth/revoke` xóa cookie, rate limit mặc định.
- FE nên tạo axios interceptor: nếu `401` → gọi refresh, cập nhật `Authorization` rồi retry một lần. TODO: bổ sung code mẫu.
- Postman variables: `{{baseUrl}}`, `{{authToken}}`, `{{csrfToken}}`. TODO: tạo collection và script tests.
- OpenAPI/Scalar: chạy ứng dụng rồi mở `/docs` (được map trong `ApplicationBuilderExtensions`). TODO: xác nhận build openapi.yaml.

## 4. Chi tiết từng Controller
### 4.1 AbuseController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/AbuseController.cs`.

#### 4.1.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| POST | `/api/abuse/report` | TODO | [Authorize] |

#### 4.1.2 Endpoint Details
##### POST /api/abuse/report — TODO mô tả
- Method + Path: `POST /api/abuse/report`
- Auth: [Authorize]
- Rate limit: BugsWrite
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `AbuseReportRequest` (xem `DTOs/Chat/AbuseReportRequest.cs:L6`)
Responses:
- [ProducesResponseType(typeof(object), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/AbuseController.cs:L16`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

### 4.2 AuthController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/AuthController.cs`.

#### 4.2.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| POST | `/api/Auth/user-register` | TODO | No (AllowAnonymous) |
| POST | `/api/Auth/login` | TODO | No (AllowAnonymous) |
| POST | `/api/Auth/refresh` | TODO | No (AllowAnonymous) |
| POST | `/api/Auth/revoke` | TODO | [Authorize] |
| POST | `/api/Auth/email:confirm` | TODO | No (AllowAnonymous) |
| POST | `/api/Auth/password:send-reset` | TODO | No (AllowAnonymous) |
| POST | `/api/Auth/password:reset` | TODO | No (AllowAnonymous) |
| GET | `/api/Auth/me` | TODO | [Authorize] |
| PUT | `/api/Auth/me` | TODO | [Authorize] |
| POST | `/api/Auth/me/password:change` | TODO | [Authorize] |
| POST | `/api/Auth/me/email:send-confirm` | TODO | [Authorize] |
| POST | `/api/Auth/me/email:send-change` | TODO | [Authorize] |

#### 4.2.2 Endpoint Details
##### POST /api/Auth/user-register — TODO mô tả
- Method + Path: `POST /api/Auth/user-register`
- Auth: No (AllowAnonymous)
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `RegisterRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L22`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/AuthController.cs:L6`

##### POST /api/Auth/login — TODO mô tả
- Method + Path: `POST /api/Auth/login`
- Auth: No (AllowAnonymous)
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `LoginRequest` (xem `DTOs/Auth/Requests/AuthRequests.cs:L5`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/AuthController.cs:L43`

##### POST /api/Auth/refresh — TODO mô tả
- Method + Path: `POST /api/Auth/refresh`
- Auth: No (AllowAnonymous)
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `RefreshTokenRequest?` (xem `DTOs/Auth/Requests/AuthRequests.cs:L13`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/AuthController.cs:L63`

##### POST /api/Auth/revoke — TODO mô tả
- Method + Path: `POST /api/Auth/revoke`
- Auth: [Authorize]
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `RevokeTokenRequest?` (xem `DTOs/Auth/Requests/AuthRequests.cs:L18`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/AuthController.cs:L88`

##### POST /api/Auth/email:confirm — TODO mô tả
- Method + Path: `POST /api/Auth/email:confirm`
- Auth: No (AllowAnonymous)
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `ConfirmEmailRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L71`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/AuthController.cs:L106`

##### POST /api/Auth/password:send-reset — TODO mô tả
- Method + Path: `POST /api/Auth/password:send-reset`
- Auth: No (AllowAnonymous)
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| callbackBaseUrl | string? | Không | — | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Auth/password:send-reset?callbackBaseUrl=...`
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `ForgotPasswordRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L59`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/AuthController.cs:L119`

##### POST /api/Auth/password:reset — TODO mô tả
- Method + Path: `POST /api/Auth/password:reset`
- Auth: No (AllowAnonymous)
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `ResetPasswordRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L63`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/AuthController.cs:L132`

##### GET /api/Auth/me — TODO mô tả
- Method + Path: `GET /api/Auth/me`
- Auth: [Authorize]
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/AuthController.cs:L139`

##### PUT /api/Auth/me — TODO mô tả
- Method + Path: `PUT /api/Auth/me`
- Auth: [Authorize]
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `UpdateUserSelfRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L45`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/AuthController.cs:L148`

##### POST /api/Auth/me/password:change — TODO mô tả
- Method + Path: `POST /api/Auth/me/password:change`
- Auth: [Authorize]
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `ChangePasswordRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L54`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/AuthController.cs:L157`

##### POST /api/Auth/me/email:send-confirm — TODO mô tả
- Method + Path: `POST /api/Auth/me/email:send-confirm`
- Auth: [Authorize]
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| callbackBaseUrl | string | Có | — | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Auth/me/email:send-confirm?callbackBaseUrl=...`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/AuthController.cs:L166`

##### POST /api/Auth/me/email:send-change — TODO mô tả
- Method + Path: `POST /api/Auth/me/email:send-change`
- Auth: [Authorize]
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| callbackBaseUrl | string | Có | — | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Auth/me/email:send-change?callbackBaseUrl=...`
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `ChangeEmailRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L76`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/AuthController.cs:L175`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

### 4.3 BugReportsController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/BugReportsController.cs`.

#### 4.3.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| POST | `/api/BugReports` | TODO | [Authorize] |
| GET | `/api/BugReports/{id:guid}` | TODO | [Authorize] |
| GET | `/api/BugReports/my` | TODO | [Authorize] |
| GET | `/api/BugReports` | TODO | [Authorize(Roles = "Admin")] |
| GET | `/api/BugReports/status/{status}` | TODO | [Authorize(Roles = "Admin")] |
| PATCH | `/api/BugReports/{id:guid}/status` | TODO | [Authorize(Roles = "Admin")] |

#### 4.3.2 Endpoint Details
##### POST /api/BugReports — TODO mô tả
- Method + Path: `POST /api/BugReports`
- Auth: [Authorize]
- Rate limit: BugsWrite
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `BugReportCreateRequest` (xem `DTOs/Bugs/BugReportCreateRequest.cs:L3`)
Responses:
- [ProducesResponseType(typeof(BugReportDto), StatusCodes.Status201Created)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/BugReportsController.cs:L7`

##### GET /api/BugReports/{id:guid} — TODO mô tả
- Method + Path: `GET /api/BugReports/{id:guid}`
- Auth: [Authorize]
- Rate limit: ReadsLight
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(BugReportDto), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/BugReportsController.cs:L48`

##### GET /api/BugReports/my — TODO mô tả
- Method + Path: `GET /api/BugReports/my`
- Auth: [Authorize]
- Rate limit: ReadsLight
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| page | int? | Không | — | TODO | TODO | TODO |
| size | int? | Không | — | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/BugReports/my?page=...&size=...`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(PagedResult<BugReportDto>), StatusCodes.Status200OK)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/BugReportsController.cs:L67`

##### GET /api/BugReports — TODO mô tả
- Method + Path: `GET /api/BugReports`
- Auth: [Authorize(Roles = "Admin")]
- Rate limit: DashboardRead
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| page | int? | Không | — | TODO | TODO | TODO |
| size | int? | Không | — | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/BugReports?page=...&size=...`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(PagedResult<BugReportDto>), StatusCodes.Status200OK)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/BugReportsController.cs:L91`

##### GET /api/BugReports/status/{status} — TODO mô tả
- Method + Path: `GET /api/BugReports/status/{status}`
- Auth: [Authorize(Roles = "Admin")]
- Rate limit: DashboardRead
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| status | string | Có | TODO: mô tả |
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| page | int? | Không | — | TODO | TODO | TODO |
| size | int? | Không | — | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/BugReports/status/{status}?page=...&size=...`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(PagedResult<BugReportDto>), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/BugReportsController.cs:L113`

##### PATCH /api/BugReports/{id:guid}/status — TODO mô tả
- Method + Path: `PATCH /api/BugReports/{id:guid}/status`
- Auth: [Authorize(Roles = "Admin")]
- Rate limit: ReadsLight
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `BugReportStatusPatchRequest` (xem `DTOs/Bugs/BugReportStatusPatchRequest.cs:L3`)
Responses:
- [ProducesResponseType(typeof(BugReportDto), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/BugReportsController.cs:L136`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

### 4.4 ClubsController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/ClubsController.cs`.

#### 4.4.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| GET | `/api/communities/{communityId:guid}/clubs` | TODO | [Authorize] |
| POST | `/api/Clubs` | TODO | [Authorize] |
| GET | `/api/Clubs/{id:guid}` | TODO | [Authorize] |
| PATCH | `/api/Clubs/{id:guid}` | TODO | [Authorize] |
| DELETE | `/api/Clubs/{id:guid}` | TODO | [Authorize] |

#### 4.4.2 Endpoint Details
##### GET /api/communities/{communityId:guid}/clubs — TODO mô tả
- Method + Path: `GET /api/communities/{communityId:guid}/clubs`
- Auth: [Authorize]
- Rate limit: ClubsRead
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| communityId | Guid | Có | TODO: mô tả |
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| name | string? | Không | null | TODO | TODO | TODO |
| isPublic | bool? | Không | null | TODO | TODO | TODO |
| membersFrom | int? | Không | null | TODO | TODO | TODO |
| membersTo | int? | Không | null | TODO | TODO | TODO |
| cursor | string? | Không | null | TODO | TODO | TODO |
| size | int | Không | 20 | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/communities/{communityId:guid}/clubs?name=null&isPublic=null&membersFrom=null`
- Ví dụ URL 2: `/api/communities/{communityId:guid}/clubs?membersTo=null&cursor=null&size=20`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(CursorPageResult<ClubBriefDto>), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/ClubsController.cs:L10`

##### POST /api/Clubs — TODO mô tả
- Method + Path: `POST /api/Clubs`
- Auth: [Authorize]
- Rate limit: ClubsWrite
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `ClubCreateRequestDto` (xem `DTOs/Clubs/ClubCreateRequestDto.cs:L7`)
Responses:
- [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/ClubsController.cs:L94`

##### GET /api/Clubs/{id:guid} — TODO mô tả
- Method + Path: `GET /api/Clubs/{id:guid}`
- Auth: [Authorize]
- Rate limit: ClubsRead
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(ClubDetailDto), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/ClubsController.cs:L128`

##### PATCH /api/Clubs/{id:guid} — TODO mô tả
- Method + Path: `PATCH /api/Clubs/{id:guid}`
- Auth: [Authorize]
- Rate limit: ClubsWrite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `ClubUpdateRequestDto` (xem `DTOs/Clubs/ClubUpdateRequestDto.cs:L6`)
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/ClubsController.cs:L145`

##### DELETE /api/Clubs/{id:guid} — TODO mô tả
- Method + Path: `DELETE /api/Clubs/{id:guid}`
- Auth: [Authorize]
- Rate limit: ClubsWrite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/ClubsController.cs:L169`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

### 4.5 CommunitiesController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/CommunitiesController.cs`.

#### 4.5.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| POST | `/api/Communities` | TODO | [Authorize] |
| GET | `/api/Communities` | TODO | [Authorize] |
| GET | `/api/Communities/{id:guid}` | TODO | [Authorize] |
| PATCH | `/api/Communities/{id:guid}` | TODO | [Authorize] |
| DELETE | `/api/Communities/{id:guid}` | TODO | [Authorize] |
| GET | `/api/Communities/discover` | TODO | No (AllowAnonymous) |

#### 4.5.2 Endpoint Details
##### POST /api/Communities — TODO mô tả
- Method + Path: `POST /api/Communities`
- Auth: [Authorize]
- Rate limit: CommunitiesWrite
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `CommunityCreateRequestDto` (xem `DTOs/Communities/CommunityCreateRequestDto.cs:L11`)
Responses:
- [ProducesResponseType(typeof(object), StatusCodes.Status201Created)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/CommunitiesController.cs:L10`

##### GET /api/Communities — TODO mô tả
- Method + Path: `GET /api/Communities`
- Auth: [Authorize]
- Rate limit: CommunitiesRead
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| school | string? | Không | null | TODO | TODO | TODO |
| gameId | Guid? | Không | null | TODO | TODO | TODO |
| isPublic | bool? | Không | null | TODO | TODO | TODO |
| membersFrom | int? | Không | null | TODO | TODO | TODO |
| membersTo | int? | Không | null | TODO | TODO | TODO |
| cursor | string? | Không | null | TODO | TODO | TODO |
| size | int | Không | 20 | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Communities?school=null&gameId=null&isPublic=null`
- Ví dụ URL 2: `/api/Communities?membersTo=null&cursor=null&size=20`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(CursorPageResult<CommunityBriefDto>), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/CommunitiesController.cs:L75`

##### GET /api/Communities/{id:guid} — TODO mô tả
- Method + Path: `GET /api/Communities/{id:guid}`
- Auth: [Authorize]
- Rate limit: CommunitiesRead
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(CommunityDetailDto), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/CommunitiesController.cs:L123`

##### PATCH /api/Communities/{id:guid} — TODO mô tả
- Method + Path: `PATCH /api/Communities/{id:guid}`
- Auth: [Authorize]
- Rate limit: CommunitiesWrite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `CommunityUpdateRequestDto` (xem `DTOs/Communities/CommunityUpdateRequestDto.cs:L10`)
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/CommunitiesController.cs:L145`

##### DELETE /api/Communities/{id:guid} — TODO mô tả
- Method + Path: `DELETE /api/Communities/{id:guid}`
- Auth: [Authorize]
- Rate limit: CommunitiesWrite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/CommunitiesController.cs:L172`

##### GET /api/Communities/discover — TODO mô tả
- Method + Path: `GET /api/Communities/discover`
- Auth: No (AllowAnonymous)
- Rate limit: CommunitiesRead
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| school | string? | Không | null | TODO | TODO | TODO |
| gameId | Guid? | Không | null | TODO | TODO | TODO |
| cursor | string? | Không | null | TODO | TODO | TODO |
| size | int? | Không | null | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Communities/discover?school=null&gameId=null&cursor=null`
- Ví dụ URL 2: `/api/Communities/discover?gameId=null&cursor=null&size=null`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(DiscoverResponse), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/CommunitiesController.cs:L208`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

### 4.6 DashboardController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/DashboardController.cs`.

#### 4.6.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| GET | `/api/Dashboard/today` | TODO | [Authorize] |

#### 4.6.2 Endpoint Details
##### GET /api/Dashboard/today — TODO mô tả
- Method + Path: `GET /api/Dashboard/today`
- Auth: [Authorize]
- Rate limit: DashboardRead
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(DashboardTodayDto), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/DashboardController.cs:L12`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

### 4.7 EventsController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/EventsController.cs`.

#### 4.7.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| POST | `/api/Events` | TODO | [Authorize] |
| POST | `/api/Events/{eventId:guid}/open` | TODO | [Authorize] |
| POST | `/api/Events/{eventId:guid}/cancel` | TODO | [Authorize] |
| GET | `/api/Events/{eventId:guid}` | TODO | [Authorize] |
| GET | `/api/Events` | TODO | [Authorize] |
| GET | `/api/organizer/events` | TODO | [Authorize] |

#### 4.7.2 Endpoint Details
##### POST /api/Events — TODO mô tả
- Method + Path: `POST /api/Events`
- Auth: [Authorize]
- Rate limit: EventsWrite
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `EventCreateRequestDto` (xem `DTOs/Events/EventCreateRequestDto.cs:L3`)
Responses:
- [ProducesResponseType(typeof(object), StatusCodes.Status201Created)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/EventsController.cs:L5`

##### POST /api/Events/{eventId:guid}/open — TODO mô tả
- Method + Path: `POST /api/Events/{eventId:guid}/open`
- Auth: [Authorize]
- Rate limit: EventsWrite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| eventId | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/EventsController.cs:L57`

##### POST /api/Events/{eventId:guid}/cancel — TODO mô tả
- Method + Path: `POST /api/Events/{eventId:guid}/cancel`
- Auth: [Authorize]
- Rate limit: EventsWrite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| eventId | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/EventsController.cs:L85`

##### GET /api/Events/{eventId:guid} — TODO mô tả
- Method + Path: `GET /api/Events/{eventId:guid}`
- Auth: [Authorize]
- Rate limit: ReadsLight
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| eventId | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(EventDetailDto), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/EventsController.cs:L107`

##### GET /api/Events — TODO mô tả
- Method + Path: `GET /api/Events`
- Auth: [Authorize]
- Rate limit: ReadsHeavy
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| statuses | IEnumerable<EventStatus>? | Không | — | TODO | TODO | TODO |
| communityId | Guid? | Không | — | TODO | TODO | TODO |
| organizerId | Guid? | Không | — | TODO | TODO | TODO |
| from | DateTimeOffset? | Không | — | TODO | TODO | TODO |
| to | DateTimeOffset? | Không | — | TODO | TODO | TODO |
| search | string? | Không | — | TODO | TODO | TODO |
| sort | string? | Không | — | TODO | TODO | TODO |
| page | int | Không | 1 | TODO | TODO | TODO |
| pageSize | int | Không | PaginationOptions.DefaultPageSize | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Events?statuses=...&communityId=...&organizerId=...`
- Ví dụ URL 2: `/api/Events?sort=...&page=1&pageSize=PaginationOptions.DefaultPageSize`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(PagedResponse<EventDetailDto>), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/EventsController.cs:L127`

##### GET /api/organizer/events — TODO mô tả
- Method + Path: `GET /api/organizer/events`
- Auth: [Authorize]
- Rate limit: ReadsHeavy
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| statuses | IEnumerable<EventStatus>? | Không | — | TODO | TODO | TODO |
| communityId | Guid? | Không | — | TODO | TODO | TODO |
| from | DateTimeOffset? | Không | — | TODO | TODO | TODO |
| to | DateTimeOffset? | Không | — | TODO | TODO | TODO |
| search | string? | Không | — | TODO | TODO | TODO |
| sort | string? | Không | — | TODO | TODO | TODO |
| page | int | Không | 1 | TODO | TODO | TODO |
| pageSize | int | Không | PaginationOptions.DefaultPageSize | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/organizer/events?statuses=...&communityId=...&from=...`
- Ví dụ URL 2: `/api/organizer/events?sort=...&page=1&pageSize=PaginationOptions.DefaultPageSize`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(PagedResponse<EventDetailDto>), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/EventsController.cs:L180`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

### 4.8 FriendsController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/FriendsController.cs`.

#### 4.8.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| POST | `/api/Friends/{userId:guid}/invite` | TODO | [Authorize] |
| POST | `/api/Friends/{userId:guid}/accept` | TODO | [Authorize] |
| POST | `/api/Friends/{userId:guid}/decline` | TODO | [Authorize] |
| POST | `/api/Friends/{userId:guid}/cancel` | TODO | [Authorize] |
| GET | `/api/Friends` | TODO | [Authorize] |

#### 4.8.2 Endpoint Details
##### POST /api/Friends/{userId:guid}/invite — TODO mô tả
- Method + Path: `POST /api/Friends/{userId:guid}/invite`
- Auth: [Authorize]
- Rate limit: FriendInvite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| userId | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/FriendsController.cs:L8`

##### POST /api/Friends/{userId:guid}/accept — TODO mô tả
- Method + Path: `POST /api/Friends/{userId:guid}/accept`
- Auth: [Authorize]
- Rate limit: FriendAction
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| userId | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/FriendsController.cs:L42`

##### POST /api/Friends/{userId:guid}/decline — TODO mô tả
- Method + Path: `POST /api/Friends/{userId:guid}/decline`
- Auth: [Authorize]
- Rate limit: FriendAction
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| userId | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/FriendsController.cs:L65`

##### POST /api/Friends/{userId:guid}/cancel — TODO mô tả
- Method + Path: `POST /api/Friends/{userId:guid}/cancel`
- Auth: [Authorize]
- Rate limit: FriendAction
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| userId | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/FriendsController.cs:L88`

##### GET /api/Friends — TODO mô tả
- Method + Path: `GET /api/Friends`
- Auth: [Authorize]
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| filter | string? | Không | — | TODO | TODO | TODO |
| request | CursorRequest | Có | — | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Friends?filter=...&request=...`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(CursorPageResult<FriendDto>), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/FriendsController.cs:L111`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

### 4.9 GoogleAuthController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/GoogleAuthController.cs`.

#### 4.9.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| POST | `/api/GoogleAuth/login` | TODO | No (AllowAnonymous) |

#### 4.9.2 Endpoint Details
##### POST /api/GoogleAuth/login — TODO mô tả
- Method + Path: `POST /api/GoogleAuth/login`
- Auth: No (AllowAnonymous)
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `GoogleLoginRequest` (xem `DTOs/Auth/Requests/GoogleLoginRequest.cs:L5`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/GoogleAuthController.cs:L5`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

### 4.10 PaymentsController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/PaymentsController.cs`.

#### 4.10.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| POST | `/api/Payments/{intentId:guid}/confirm` | TODO | [Authorize] |
| GET | `/api/Payments/{intentId:guid}` | TODO | [Authorize] |
| POST | `/api/Payments/{intentId:guid}/vnpay/checkout` | TODO | [Authorize] |
| POST | `/api/Payments/webhooks/vnpay` | TODO | No (AllowAnonymous) |
| GET | `/api/Payments/vnpay/return` | TODO | No (AllowAnonymous) |

#### 4.10.2 Endpoint Details
##### POST /api/Payments/{intentId:guid}/confirm — TODO mô tả
- Method + Path: `POST /api/Payments/{intentId:guid}/confirm`
- Auth: [Authorize]
- Rate limit: PaymentsWrite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| intentId | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/PaymentsController.cs:L6`

##### GET /api/Payments/{intentId:guid} — TODO mô tả
- Method + Path: `GET /api/Payments/{intentId:guid}`
- Auth: [Authorize]
- Rate limit: ReadsLight
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| intentId | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(PaymentIntentDto), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/PaymentsController.cs:L42`

##### POST /api/Payments/{intentId:guid}/vnpay/checkout — TODO mô tả
- Method + Path: `POST /api/Payments/{intentId:guid}/vnpay/checkout`
- Auth: [Authorize]
- Rate limit: PaymentsWrite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| intentId | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `VnPayCheckoutRequest?` (chưa tìm thấy định nghĩa)
Responses:
- [ProducesResponseType(typeof(string), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/PaymentsController.cs:L62`

##### POST /api/Payments/webhooks/vnpay — TODO mô tả
- Method + Path: `POST /api/Payments/webhooks/vnpay`
- Auth: No (AllowAnonymous)
- Rate limit: PaymentsWebhook
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(VnPayWebhookResponse), StatusCodes.Status200OK)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/PaymentsController.cs:L87`

##### GET /api/Payments/vnpay/return — TODO mô tả
- Method + Path: `GET /api/Payments/vnpay/return`
- Auth: No (AllowAnonymous)
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status302Found)] (TODO mô tả schema)
- [ProducesResponseType(StatusCodes.Status400BadRequest)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/PaymentsController.cs:L113`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

### 4.11 QuestsController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/QuestsController.cs`.

#### 4.11.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| GET | `/api/Quests/today` | TODO | [Authorize] |
| POST | `/api/Quests/check-in` | TODO | [Authorize] |
| POST | `/api/Quests/join-room/{roomId:guid}` | TODO | [Authorize] |
| POST | `/api/Quests/attend-event/{eventId:guid}` | TODO | [Authorize] |

#### 4.11.2 Endpoint Details
##### GET /api/Quests/today — TODO mô tả
- Method + Path: `GET /api/Quests/today`
- Auth: [Authorize]
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(QuestTodayDto), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/QuestsController.cs:L15`

##### POST /api/Quests/check-in — TODO mô tả
- Method + Path: `POST /api/Quests/check-in`
- Auth: [Authorize]
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/QuestsController.cs:L51`

##### POST /api/Quests/join-room/{roomId:guid} — TODO mô tả
- Method + Path: `POST /api/Quests/join-room/{roomId:guid}`
- Auth: [Authorize]
- Rate limit: Không thiết lập cụ thể
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| roomId | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/QuestsController.cs:L73`

##### POST /api/Quests/attend-event/{eventId:guid} — TODO mô tả
- Method + Path: `POST /api/Quests/attend-event/{eventId:guid}`
- Auth: [Authorize]
- Rate limit: Không thiết lập cụ thể
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| eventId | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/QuestsController.cs:L96`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

### 4.12 RegistrationsController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/RegistrationsController.cs`.

#### 4.12.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| POST | `/api/events/{eventId:guid}/registrations` | TODO | [Authorize] |
| GET | `/api/events/{eventId:guid}/registrations` | TODO | [Authorize] |
| GET | `/api/me/registrations` | TODO | [Authorize] |

#### 4.12.2 Endpoint Details
##### POST /api/events/{eventId:guid}/registrations — TODO mô tả
- Method + Path: `POST /api/events/{eventId:guid}/registrations`
- Auth: [Authorize]
- Rate limit: RegistrationsWrite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| eventId | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(object), StatusCodes.Status201Created)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RegistrationsController.cs:L7`

##### GET /api/events/{eventId:guid}/registrations — TODO mô tả
- Method + Path: `GET /api/events/{eventId:guid}/registrations`
- Auth: [Authorize]
- Rate limit: ReadsHeavy
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| eventId | Guid | Có | TODO: mô tả |
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| statuses | IEnumerable<EventRegistrationStatus>? | Không | — | TODO | TODO | TODO |
| page | int | Không | 1 | TODO | TODO | TODO |
| pageSize | int | Không | PaginationOptions.DefaultPageSize | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/events/{eventId:guid}/registrations?statuses=...&page=1&pageSize=PaginationOptions.DefaultPageSize`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(PagedResponse<RegistrationListItemDto>), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RegistrationsController.cs:L56`

##### GET /api/me/registrations — TODO mô tả
- Method + Path: `GET /api/me/registrations`
- Auth: [Authorize]
- Rate limit: ReadsLight
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| statuses | IEnumerable<EventRegistrationStatus>? | Không | — | TODO | TODO | TODO |
| page | int | Không | 1 | TODO | TODO | TODO |
| pageSize | int | Không | PaginationOptions.DefaultPageSize | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/me/registrations?statuses=...&page=1&pageSize=PaginationOptions.DefaultPageSize`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(PagedResponse<MyRegistrationDto>), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RegistrationsController.cs:L89`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

### 4.13 RolesController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/RolesController.cs`.

#### 4.13.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| GET | `/api/Roles` | TODO | No |
| GET | `/api/Roles/exists` | TODO | No |
| POST | `/api/Roles` | TODO | No |
| PUT | `/api/Roles/{id:guid}` | TODO | No |
| DELETE | `/api/Roles/{id:guid}` | TODO | No |
| POST | `/api/Roles/{id:guid}/soft-delete` | TODO | No |
| POST | `/api/Roles/{id:guid}/restore` | TODO | No |
| POST | `/api/Roles/batch-create` | TODO | No |
| PUT | `/api/Roles/batch-update` | TODO | No |
| DELETE | `/api/Roles/batch-delete` | TODO | No |
| POST | `/api/Roles/batch-soft-delete` | TODO | No |
| POST | `/api/Roles/batch-restore` | TODO | No |

#### 4.13.2 Endpoint Details
##### GET /api/Roles — TODO mô tả
- Method + Path: `GET /api/Roles`
- Auth: No
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| paging | PageRequest | Có | — | TODO | TODO | TODO |
| filter | RoleFilter | Có | — | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Roles?paging=...&filter=...`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(PagedResult<RoleDto>), StatusCodes.Status200OK)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RolesController.cs:L3`

##### GET /api/Roles/exists — TODO mô tả
- Method + Path: `GET /api/Roles/exists`
- Auth: No
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| name | string | Có | — | TODO | TODO | TODO |
| excludeId | Guid? | Không | — | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Roles/exists?name=...&excludeId=...`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RolesController.cs:L35`

##### POST /api/Roles — TODO mô tả
- Method + Path: `POST /api/Roles`
- Auth: No
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `CreateRoleRequest` (xem `DTOs/Roles/Requests/RoleRequests.cs:L3`)
Responses:
- [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)] (TODO mô tả schema)
- [ProducesResponseType(StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(StatusCodes.Status409Conflict)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RolesController.cs:L46`

##### PUT /api/Roles/{id:guid} — TODO mô tả
- Method + Path: `PUT /api/Roles/{id:guid}`
- Auth: No
- Rate limit: Không thiết lập cụ thể
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `UpdateRoleRequest` (xem `DTOs/Roles/Requests/RoleRequests.cs:L8`)
Responses:
- [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(StatusCodes.Status409Conflict)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RolesController.cs:L62`

##### DELETE /api/Roles/{id:guid} — TODO mô tả
- Method + Path: `DELETE /api/Roles/{id:guid}`
- Auth: No
- Rate limit: Không thiết lập cụ thể
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(StatusCodes.Status404NotFound)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RolesController.cs:L74`

##### POST /api/Roles/{id:guid}/soft-delete — TODO mô tả
- Method + Path: `POST /api/Roles/{id:guid}/soft-delete`
- Auth: No
- Rate limit: Không thiết lập cụ thể
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(StatusCodes.Status404NotFound)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RolesController.cs:L84`

##### POST /api/Roles/{id:guid}/restore — TODO mô tả
- Method + Path: `POST /api/Roles/{id:guid}/restore`
- Auth: No
- Rate limit: Không thiết lập cụ thể
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(StatusCodes.Status404NotFound)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RolesController.cs:L94`

##### POST /api/Roles/batch-create — TODO mô tả
- Method + Path: `POST /api/Roles/batch-create`
- Auth: No
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `IEnumerable<CreateRoleRequest>` (chưa tìm thấy định nghĩa)
Responses:
- [ProducesResponseType(typeof(BatchResult<Guid, RoleDto>), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(StatusCodes.Status409Conflict)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RolesController.cs:L106`

##### PUT /api/Roles/batch-update — TODO mô tả
- Method + Path: `PUT /api/Roles/batch-update`
- Auth: No
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `` (chưa tìm thấy định nghĩa)
Responses:
- [ProducesResponseType(typeof(BatchResult<Guid, RoleDto>), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(StatusCodes.Status409Conflict)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RolesController.cs:L117`

##### DELETE /api/Roles/batch-delete — TODO mô tả
- Method + Path: `DELETE /api/Roles/batch-delete`
- Auth: No
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `IEnumerable<Guid>` (chưa tìm thấy định nghĩa)
Responses:
- [ProducesResponseType(typeof(BatchOutcome<Guid>), StatusCodes.Status200OK)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RolesController.cs:L128`

##### POST /api/Roles/batch-soft-delete — TODO mô tả
- Method + Path: `POST /api/Roles/batch-soft-delete`
- Auth: No
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `IEnumerable<Guid>` (chưa tìm thấy định nghĩa)
Responses:
- [ProducesResponseType(typeof(BatchOutcome<Guid>), StatusCodes.Status200OK)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RolesController.cs:L137`

##### POST /api/Roles/batch-restore — TODO mô tả
- Method + Path: `POST /api/Roles/batch-restore`
- Auth: No
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `IEnumerable<Guid>` (chưa tìm thấy định nghĩa)
Responses:
- [ProducesResponseType(typeof(BatchOutcome<Guid>), StatusCodes.Status200OK)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RolesController.cs:L146`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

### 4.14 RoomsController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/RoomsController.cs`.

#### 4.14.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| GET | `/api/Rooms/{id:guid}` | TODO | [Authorize] |
| GET | `/api/Rooms/{id:guid}/members` | TODO | [Authorize] |
| POST | `/api/Rooms` | TODO | [Authorize] |
| POST | `/api/Rooms/{id:guid}/join` | TODO | [Authorize] |
| POST | `/api/Rooms/{id:guid}/approve/{userId:guid}` | TODO | [Authorize] |
| POST | `/api/Rooms/{id:guid}/leave` | TODO | [Authorize] |
| POST | `/api/Rooms/{id:guid}/kickban/{userId:guid}` | TODO | [Authorize] |
| PATCH | `/api/Rooms/{id:guid}` | TODO | [Authorize] |
| POST | `/api/Rooms/{id:guid}/transfer-ownership/{newOwnerId:guid}` | TODO | [Authorize] |
| DELETE | `/api/Rooms/{id:guid}` | TODO | [Authorize] |

#### 4.14.2 Endpoint Details
##### GET /api/Rooms/{id:guid} — TODO mô tả
- Method + Path: `GET /api/Rooms/{id:guid}`
- Auth: [Authorize]
- Rate limit: RoomsRead
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(RoomDetailDto), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RoomsController.cs:L10`

##### GET /api/Rooms/{id:guid}/members — TODO mô tả
- Method + Path: `GET /api/Rooms/{id:guid}/members`
- Auth: [Authorize]
- Rate limit: RoomsRead
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| skip | int | Không | 0 | TODO | TODO | TODO |
| take | int | Không | 20 | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Rooms/{id:guid}/members?skip=0&take=20`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(IReadOnlyList<RoomMemberBriefDto>), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RoomsController.cs:L54`

##### POST /api/Rooms — TODO mô tả
- Method + Path: `POST /api/Rooms`
- Auth: [Authorize]
- Rate limit: RoomsCreate
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `RoomCreateRequestDto` (xem `DTOs/Rooms/RoomCreateRequestDto.cs:L6`)
Responses:
- [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RoomsController.cs:L83`

##### POST /api/Rooms/{id:guid}/join — TODO mô tả
- Method + Path: `POST /api/Rooms/{id:guid}/join`
- Auth: [Authorize]
- Rate limit: RoomsWrite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `RoomJoinRequestDto` (xem `DTOs/Rooms/RoomJoinRequestDto.cs:L6`)
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RoomsController.cs:L135`

##### POST /api/Rooms/{id:guid}/approve/{userId:guid} — TODO mô tả
- Method + Path: `POST /api/Rooms/{id:guid}/approve/{userId:guid}`
- Auth: [Authorize]
- Rate limit: RoomsWrite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
| userId | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RoomsController.cs:L177`

##### POST /api/Rooms/{id:guid}/leave — TODO mô tả
- Method + Path: `POST /api/Rooms/{id:guid}/leave`
- Auth: [Authorize]
- Rate limit: RoomsWrite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RoomsController.cs:L217`

##### POST /api/Rooms/{id:guid}/kickban/{userId:guid} — TODO mô tả
- Method + Path: `POST /api/Rooms/{id:guid}/kickban/{userId:guid}`
- Auth: [Authorize]
- Rate limit: RoomsWrite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
| userId | Guid | Có | TODO: mô tả |
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| ban | bool | Không | false | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Rooms/{id:guid}/kickban/{userId:guid}?ban=false`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RoomsController.cs:L256`

##### PATCH /api/Rooms/{id:guid} — TODO mô tả
- Method + Path: `PATCH /api/Rooms/{id:guid}`
- Auth: [Authorize]
- Rate limit: RoomsWrite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `RoomUpdateRequestDto` (xem `DTOs/Rooms/RoomUpdateRequestDto.cs:L6`)
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RoomsController.cs:L298`

##### POST /api/Rooms/{id:guid}/transfer-ownership/{newOwnerId:guid} — TODO mô tả
- Method + Path: `POST /api/Rooms/{id:guid}/transfer-ownership/{newOwnerId:guid}`
- Auth: [Authorize]
- Rate limit: RoomsWrite
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
| newOwnerId | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RoomsController.cs:L334`

##### DELETE /api/Rooms/{id:guid} — TODO mô tả
- Method + Path: `DELETE /api/Rooms/{id:guid}`
- Auth: [Authorize]
- Rate limit: RoomsArchive
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(StatusCodes.Status204NoContent)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/RoomsController.cs:L367`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

### 4.15 TeammatesController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/TeammatesController.cs`.

#### 4.15.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| GET | `/api/Teammates` | TODO | [Authorize] |

#### 4.15.2 Endpoint Details
##### GET /api/Teammates — TODO mô tả
- Method + Path: `GET /api/Teammates`
- Auth: [Authorize]
- Rate limit: TeammatesRead
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| gameId | Guid? | Không | — | TODO | TODO | TODO |
| university | string? | Không | — | TODO | TODO | TODO |
| skill | GameSkillLevel? | Không | — | TODO | TODO | TODO |
| onlineOnly | bool | Không | false | TODO | TODO | TODO |
| cursor | string? | Không | null | TODO | TODO | TODO |
| size | int | Không | 20 | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Teammates?gameId=...&university=...&skill=...`
- Ví dụ URL 2: `/api/Teammates?onlineOnly=false&cursor=null&size=20`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- [ProducesResponseType(typeof(CursorPageResult<TeammateDto>), StatusCodes.Status200OK)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)] (TODO mô tả schema)
- [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)] (TODO mô tả schema)
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/TeammatesController.cs:L12`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

### 4.16 UsersController — Overview
TODO: Tóm tắt chức năng chính. Tham khảo `WebAPI/Controllers/UsersController.cs`.

#### 4.16.1 Endpoint Catalog
| Method | Path | Mô tả ngắn | Auth? |
| --- | --- | --- | --- |
| GET | `/api/Users` | TODO | [Authorize(Roles = "Admin")] |
| GET | `/api/Users/{id:guid}` | TODO | [Authorize(Roles = "Admin")] |
| POST | `/api/Users` | TODO | [Authorize(Roles = "Admin")] |
| PUT | `/api/Users/{id:guid}` | TODO | [Authorize(Roles = "Admin")] |
| PATCH | `/api/Users/{id:guid}/lockout` | TODO | [Authorize(Roles = "Admin")] |
| POST | `/api/Users/{id:guid}/roles:replace` | TODO | [Authorize(Roles = "Admin")] |
| POST | `/api/Users/{id:guid}/roles:modify` | TODO | [Authorize(Roles = "Admin")] |
| POST | `/api/Users/{id:guid}/password:change` | TODO | [Authorize(Roles = "Admin")] |
| POST | `/api/Users/password:send-reset` | TODO | No (AllowAnonymous) |
| POST | `/api/Users/password:reset` | TODO | No (AllowAnonymous) |
| POST | `/api/Users/{id:guid}/email:send-confirm` | TODO | [Authorize(Roles = "Admin")] |
| POST | `/api/Users/email:confirm` | TODO | No (AllowAnonymous) |
| POST | `/api/Users/{id:guid}/email:send-change` | TODO | [Authorize(Roles = "Admin")] |
| POST | `/api/Users/email:confirm-change` | TODO | No (AllowAnonymous) |

#### 4.16.2 Endpoint Details
##### GET /api/Users — TODO mô tả
- Method + Path: `GET /api/Users`
- Auth: [Authorize(Roles = "Admin")]
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| filter | UserFilter | Có | — | TODO | TODO | TODO |
| page | PageRequest | Có | — | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Users?filter=...&page=...`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/UsersController.cs:L5`

##### GET /api/Users/{id:guid} — TODO mô tả
- Method + Path: `GET /api/Users/{id:guid}`
- Auth: [Authorize(Roles = "Admin")]
- Rate limit: Không thiết lập cụ thể
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/UsersController.cs:L23`

##### POST /api/Users — TODO mô tả
- Method + Path: `POST /api/Users`
- Auth: [Authorize(Roles = "Admin")]
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `CreateUserAdminRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L6`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/UsersController.cs:L32`

##### PUT /api/Users/{id:guid} — TODO mô tả
- Method + Path: `PUT /api/Users/{id:guid}`
- Auth: [Authorize(Roles = "Admin")]
- Rate limit: Không thiết lập cụ thể
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `UpdateUserRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L32`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/UsersController.cs:L41`

##### PATCH /api/Users/{id:guid}/lockout — TODO mô tả
- Method + Path: `PATCH /api/Users/{id:guid}/lockout`
- Auth: [Authorize(Roles = "Admin")]
- Rate limit: Không thiết lập cụ thể
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `SetLockoutRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L69`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/UsersController.cs:L50`

##### POST /api/Users/{id:guid}/roles:replace — TODO mô tả
- Method + Path: `POST /api/Users/{id:guid}/roles:replace`
- Auth: [Authorize(Roles = "Admin")]
- Rate limit: Không thiết lập cụ thể
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `ReplaceRolesRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L90`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/UsersController.cs:L59`

##### POST /api/Users/{id:guid}/roles:modify — TODO mô tả
- Method + Path: `POST /api/Users/{id:guid}/roles:modify`
- Auth: [Authorize(Roles = "Admin")]
- Rate limit: Không thiết lập cụ thể
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `ModifyRolesRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L87`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/UsersController.cs:L67`

##### POST /api/Users/{id:guid}/password:change — TODO mô tả
- Method + Path: `POST /api/Users/{id:guid}/password:change`
- Auth: [Authorize(Roles = "Admin")]
- Rate limit: Không thiết lập cụ thể
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `ChangePasswordRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L54`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/UsersController.cs:L76`

##### POST /api/Users/password:send-reset — TODO mô tả
- Method + Path: `POST /api/Users/password:send-reset`
- Auth: No (AllowAnonymous)
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| callbackBaseUrl | string | Có | — | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Users/password:send-reset?callbackBaseUrl=...`
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `ForgotPasswordRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L59`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/UsersController.cs:L84`

##### POST /api/Users/password:reset — TODO mô tả
- Method + Path: `POST /api/Users/password:reset`
- Auth: No (AllowAnonymous)
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `ResetPasswordRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L63`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/UsersController.cs:L93`

##### POST /api/Users/{id:guid}/email:send-confirm — TODO mô tả
- Method + Path: `POST /api/Users/{id:guid}/email:send-confirm`
- Auth: [Authorize(Roles = "Admin")]
- Rate limit: Không thiết lập cụ thể
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| callbackBaseUrl | string | Có | — | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Users/{id:guid}/email:send-confirm?callbackBaseUrl=...`
Headers: _Không có đặc thù_
Request Body: _Không có_
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/UsersController.cs:L103`

##### POST /api/Users/email:confirm — TODO mô tả
- Method + Path: `POST /api/Users/email:confirm`
- Auth: No (AllowAnonymous)
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `ConfirmEmailRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L71`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/UsersController.cs:L111`

##### POST /api/Users/{id:guid}/email:send-change — TODO mô tả
- Method + Path: `POST /api/Users/{id:guid}/email:send-change`
- Auth: [Authorize(Roles = "Admin")]
- Rate limit: Không thiết lập cụ thể
Path Params:
| Param | Kiểu | Required | Ghi chú |
| --- | --- | --- | --- |
| id | Guid | Có | TODO: mô tả |
Query Params:
| Param | Kiểu | Required | Default | Toán tử | Validation | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| callbackBaseUrl | string | Có | — | TODO | TODO | TODO |
- Ví dụ URL 1: `/api/Users/{id:guid}/email:send-change?callbackBaseUrl=...`
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `ChangeEmailRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L76`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/UsersController.cs:L121`

##### POST /api/Users/email:confirm-change — TODO mô tả
- Method + Path: `POST /api/Users/email:confirm-change`
- Auth: No (AllowAnonymous)
- Rate limit: Không thiết lập cụ thể
Path Params: _Không có_
Query Params: _Không có_
Headers: _Không có đặc thù_
Request Body schema:
- TODO: Mô tả `ConfirmChangeEmailRequest` (xem `DTOs/Users/Requests/UserRequest.cs:L81`)
Responses:
- TODO: liệt kê 200/4xx/5xx từ controller/service
VÍ DỤ FE:
- TODO: cURL
- TODO: Postman
- TODO: fetch/axios
Ghi chú kỹ thuật: TODO (timezone, idempotency, v.v.)
Code reference: `WebAPI/Controllers/UsersController.cs:L129`

FE Recipes: TODO bổ sung snippet phổ biến cho controller này.

## 5. SignalR
- TODO: Kiểm tra `WebAPI/Hubs` (PresenceHub, ChatHub) và ghi rõ URL, phương thức, payload.

## 6. Rate limit & Policies
| Policy | Giới hạn | Áp dụng |
| --- | --- | --- |
| EventsWrite | 60 req/phút/người | TODO: liệt kê endpoint Events write |
| RegistrationsWrite | 60 req/phút/người | TODO |
| PaymentsWrite | 120 req/phút/người | TODO |
| PaymentsWebhook | 300 req/phút/IP/phút | TODO |
| ReadsHeavy | 300 req/phút/người | TODO |
| ReadsLight | 600 req/phút/người | TODO |
| FriendInvite | 20 req/ngày/người | TODO (Friends invites) |
| FriendAction | 60 req/phút/người | TODO |
| DashboardRead | 120 req/phút/người | TODO |
| TeammatesRead | 120 req/phút/người | `GET /api/Teammates` |
| RoomsCreate | 10 req/ngày/người | TODO |
| RoomsRead | 120 req/phút/người | TODO |
| RoomsWrite | 30 req/phút/người | TODO |
| RoomsArchive | 10 req/ngày/người | TODO |
| CommunitiesRead | 120 req/phút/người | TODO |
| CommunitiesWrite | 10 req/ngày/người | TODO |
| ClubsRead | 120 req/phút/người | ClubsController đọc |
| ClubsWrite | 10 req/ngày/người | ClubsController ghi |
| BugsWrite | 20 req/ngày/người | Abuse/BugReports POST |

## 7. Phụ lục
- TODO: liệt kê enum chính (vd `Gender`, ...) và mô tả.
- TODO: thông số phân trang mặc định cho PageRequest/CursorRequest.
- TODO: bảng mã lỗi chuẩn / mapping `Error.Codes`.

Generated At: 2025-10-15 14:55:17 UTC
Commit: af9f7a8b21ea8a08fbb7ae067813dbc341e724b8
Tổng số endpoint thống kê: 92
