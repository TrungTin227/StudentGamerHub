Student Gamer Hub API Reference (Draft)

Trạng thái: DRAFT
Generated at: 2025-10-15 14:55:17 UTC
Commit: af9f7a8b21ea8a08fbb7ae067813dbc341e724b8
Tổng số endpoint thống kê: 92

Mục lục

1. Tổng quan

2. Ma trận Bao phủ (Coverage Matrix)

3. Hướng dẫn FE tích hợp (global)

4. Chi tiết từng Controller

4.1 AbuseController

4.2 AuthController

4.3 BugReportsController

4.4 ClubsController

4.5 CommunitiesController

4.6 DashboardController

4.7 EventsController

4.8 FriendsController

4.9 GoogleAuthController

4.10 PaymentsController

4.11 QuestsController

4.12 RegistrationsController

4.13 RolesController

4.14 RoomsController

4.15 TeammatesController

4.16 UsersController

5. SignalR

6. Rate limit & Policies

7. Phụ lục

1. Tổng quan

Base URL (Development):
https://localhost:7227 hoặc http://localhost:5277
(xem WebAPI/Properties/launchSettings.json)

Authentication: Bearer JWT (Authorization: Bearer <token>) + refresh token trong cookie HttpOnly + CSRF header.
Tên cookie/header: AuthCookie.RefreshName, AuthCookie.CsrfHeader, service: CsrfService.

Time zone: Mọi timestamp trong DTO dùng UTC (các thuộc tính *AtUtc).

Error envelope: ASP.NET Core ProblemDetails. Ví dụ:

{
  "type": "https://httpstatuses.io/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "TODO: detail message",
  "instance": "/api/placeholder",
  "code": "Bad Request",
  "traceId": "00-00000000000000000000000000000000-0000000000000000-00"
}


Pagination: Offset (page, size) hoặc cursor (cursor, size, sort, desc) tùy endpoint.
TODO: điền giới hạn chi tiết từ service layer.

Filtering & sorting: Theo từng endpoint (xem bảng filter trong phần Controller).
TODO: xác nhận toán tử/validation từ service/validator.

Rate limit: Cấu hình token bucket trong WebAPI/Extensions/ServiceCollectionExtensions.cs. Chi tiết tại mục 6
.

2. Ma trận Bao phủ (Coverage Matrix)
Controller	Số endpoint	Endpoint (method + path)	Trạng thái
AbuseController	1	POST /api/abuse/report	TODO
AuthController	12	POST /api/Auth/user-register
POST /api/Auth/login
POST /api/Auth/refresh
POST /api/Auth/revoke
POST /api/Auth/email:confirm
POST /api/Auth/password:send-reset
POST /api/Auth/password:reset
GET /api/Auth/me
PUT /api/Auth/me
POST /api/Auth/me/password:change
POST /api/Auth/me/email:send-confirm
POST /api/Auth/me/email:send-change	TODO
BugReportsController	6	POST /api/BugReports
GET /api/BugReports/{id:guid}
GET /api/BugReports/my
GET /api/BugReports
GET /api/BugReports/status/{status}
PATCH /api/BugReports/{id:guid}/status	TODO
ClubsController	5	GET /api/communities/{communityId:guid}/clubs
POST /api/Clubs
GET /api/Clubs/{id:guid}
PUT /api/Clubs/{id:guid}
DELETE /api/Clubs/{id:guid}	TODO
CommunitiesController	6	POST /api/Communities
GET /api/Communities
GET /api/Communities/{id:guid}
PUT /api/Communities/{id:guid}
DELETE /api/Communities/{id:guid}
GET /api/Communities/discover	TODO
DashboardController	1	GET /api/Dashboard/today	TODO
EventsController	6	POST /api/Events
POST /api/Events/{eventId:guid}/open
POST /api/Events/{eventId:guid}/cancel
GET /api/Events/{eventId:guid}
GET /api/Events
GET /api/organizer/events	TODO
FriendsController	5	POST /api/Friends/{userId:guid}/invite
POST /api/Friends/{userId:guid}/accept
POST /api/Friends/{userId:guid}/decline
POST /api/Friends/{userId:guid}/cancel
GET /api/Friends	TODO
GoogleAuthController	1	POST /api/GoogleAuth/login	TODO
PaymentsController	5	POST /api/Payments/{intentId:guid}/confirm
GET /api/Payments/{intentId:guid}
POST /api/Payments/{intentId:guid}/vnpay/checkout
POST /api/Payments/webhooks/vnpay
GET /api/Payments/vnpay/return	TODO
QuestsController	4	GET /api/Quests/today
POST /api/Quests/check-in
POST /api/Quests/join-room/{roomId:guid}
POST /api/Quests/attend-event/{eventId:guid}	TODO
RegistrationsController	3	POST /api/events/{eventId:guid}/registrations
GET /api/events/{eventId:guid}/registrations
GET /api/me/registrations	TODO
RolesController	12	GET /api/Roles
GET /api/Roles/exists
POST /api/Roles
PUT /api/Roles/{id:guid}
DELETE /api/Roles/{id:guid}
POST /api/Roles/{id:guid}/soft-delete
POST /api/Roles/{id:guid}/restore
POST /api/Roles/batch-create
PUT /api/Roles/batch-update
DELETE /api/Roles/batch-delete
POST /api/Roles/batch-soft-delete
POST /api/Roles/batch-restore	TODO
RoomsController	10	GET /api/Rooms/{id:guid}
GET /api/Rooms/{id:guid}/members
POST /api/Rooms
POST /api/Rooms/{id:guid}/join
POST /api/Rooms/{id:guid}/approve/{userId:guid}
POST /api/Rooms/{id:guid}/leave
POST /api/Rooms/{id:guid}/kickban/{userId:guid}
PUT /api/Rooms/{id:guid}
POST /api/Rooms/{id:guid}/transfer-ownership/{newOwnerId:guid}
DELETE /api/Rooms/{id:guid}	TODO
TeammatesController	1	GET /api/Teammates	TODO
UsersController	14	GET /api/Users
GET /api/Users/{id:guid}
POST /api/Users
PUT /api/Users/{id:guid}
PATCH /api/Users/{id:guid}/lockout
POST /api/Users/{id:guid}/roles:replace
POST /api/Users/{id:guid}/roles:modify
POST /api/Users/{id:guid}/password:change
POST /api/Users/password:send-reset
POST /api/Users/password:reset
POST /api/Users/{id:guid}/email:send-confirm
POST /api/Users/email:confirm
POST /api/Users/{id:guid}/email:send-change
POST /api/Users/email:confirm-change	TODO
3. Hướng dẫn FE tích hợp (global)

Đăng nhập: POST /api/Auth/login với LoginRequest.
Server trả về access token; refresh token được set vào cookie HttpOnly. FE cần lưu CSRF token từ cookie AuthCookie.CsrfName.

Refresh token: POST /api/Auth/refresh.
Gửi refresh token (từ cookie; có thể kèm body dự phòng) + header CSRF AuthCookie.CsrfHeader.

Đăng xuất/Revoke: POST /api/Auth/revoke (xóa cookie). Rate limit mặc định.

Interceptor đề xuất:
Khi gặp 401 → gọi refresh → cập nhật Authorization → retry đúng 1 lần.
TODO: bổ sung code mẫu.

Postman: sử dụng variables {{baseUrl}}, {{authToken}}, {{csrfToken}}.
TODO: tạo collection và script tests.

OpenAPI/Scalar: chạy ứng dụng và mở /docs.
TODO: xác nhận build openapi.yaml.

4. Chi tiết từng Controller

Cách đọc phần này

Mỗi endpoint hiển thị: Signature, Auth, Rate limit, Params, Body, Responses, Code.

Mọi timestamp là UTC. Nếu không đề cập, header đặc thù = không.

4.1 AbuseController — Overview

TODO: Tóm tắt chức năng. Tham khảo WebAPI/Controllers/AbuseController.cs.

4.1.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
POST	/api/abuse/report	TODO	[Authorize]
4.1.2 Endpoint Details

POST /api/abuse/report — TODO mô tả

Auth: [Authorize]

Rate limit: BugsWrite

Path params: —

Query: —

Body DTO: AbuseReportRequest — DTOs/Chat/AbuseReportRequest.cs:L6

Responses:

200 OK: object (TODO schema)

400/401/429/500: ProblemDetails

Code: WebAPI/Controllers/AbuseController.cs:L16

4.2 AuthController — Overview

TODO: Tóm tắt. Tham khảo WebAPI/Controllers/AuthController.cs.

4.2.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
POST	/api/Auth/user-register	TODO	AllowAnonymous
POST	/api/Auth/login	TODO	AllowAnonymous
POST	/api/Auth/refresh	TODO	AllowAnonymous
POST	/api/Auth/revoke	TODO	[Authorize]
POST	/api/Auth/email:confirm	TODO	AllowAnonymous
POST	/api/Auth/password:send-reset	TODO	AllowAnonymous
POST	/api/Auth/password:reset	TODO	AllowAnonymous
GET	/api/Auth/me	TODO	[Authorize]
PUT	/api/Auth/me	TODO	[Authorize]
POST	/api/Auth/me/password:change	TODO	[Authorize]
POST	/api/Auth/me/email:send-confirm	TODO	[Authorize]
POST	/api/Auth/me/email:send-change	TODO	[Authorize]
4.2.2 Endpoint Details

POST /api/Auth/user-register — TODO

Auth: AllowAnonymous

Rate limit: —

Body DTO: RegisterRequest — DTOs/Users/Requests/UserRequest.cs:L22

Responses: 200/4xx/5xx (TODO chi tiết)

Code: WebAPI/Controllers/AuthController.cs:L6

POST /api/Auth/login — TODO

Auth: AllowAnonymous

Body DTO: LoginRequest — DTOs/Auth/Requests/AuthRequests.cs:L5

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/AuthController.cs:L43

POST /api/Auth/refresh — TODO

Auth: AllowAnonymous

Body DTO: RefreshTokenRequest? — DTOs/Auth/Requests/AuthRequests.cs:L13

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/AuthController.cs:L63

POST /api/Auth/revoke — TODO

Auth: [Authorize]

Body DTO: RevokeTokenRequest? — DTOs/Auth/Requests/AuthRequests.cs:L18

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/AuthController.cs:L88

POST /api/Auth/email:confirm — TODO

Auth: AllowAnonymous

Body DTO: ConfirmEmailRequest — DTOs/Users/Requests/UserRequest.cs:L71

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/AuthController.cs:L106

POST /api/Auth/password:send-reset — TODO

Auth: AllowAnonymous

Query: callbackBaseUrl: string?

Body DTO: ForgotPasswordRequest — DTOs/Users/Requests/UserRequest.cs:L59

Code: WebAPI/Controllers/AuthController.cs:L119

POST /api/Auth/password:reset — TODO

Auth: AllowAnonymous

Body DTO: ResetPasswordRequest — DTOs/Users/Requests/UserRequest.cs:L63

Code: WebAPI/Controllers/AuthController.cs:L132

GET /api/Auth/me — TODO

Auth: [Authorize]

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/AuthController.cs:L139

PUT /api/Auth/me — TODO

Auth: [Authorize]

Body DTO: UpdateUserSelfRequest — DTOs/Users/Requests/UserRequest.cs:L45

Code: WebAPI/Controllers/AuthController.cs:L148

POST /api/Auth/me/password:change — TODO

Auth: [Authorize]

Body DTO: ChangePasswordRequest — DTOs/Users/Requests/UserRequest.cs:L54

Code: WebAPI/Controllers/AuthController.cs:L157

POST /api/Auth/me/email:send-confirm — TODO

Auth: [Authorize]

Query: callbackBaseUrl: string (required)

Code: WebAPI/Controllers/AuthController.cs:L166

POST /api/Auth/me/email:send-change — TODO

Auth: [Authorize]

Query: callbackBaseUrl: string (required)

Body DTO: ChangeEmailRequest — DTOs/Users/Requests/UserRequest.cs:L76

Code: WebAPI/Controllers/AuthController.cs:L175

4.3 BugReportsController — Overview

TODO. Tham khảo WebAPI/Controllers/BugReportsController.cs.

4.3.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
POST	/api/BugReports	TODO	[Authorize]
GET	/api/BugReports/{id:guid}	TODO	[Authorize]
GET	/api/BugReports/my	TODO	[Authorize]
GET	/api/BugReports	TODO	[Authorize(Roles = "Admin")]
GET	/api/BugReports/status/{status}	TODO	[Authorize(Roles = "Admin")]
PATCH	/api/BugReports/{id:guid}/status	TODO	[Authorize(Roles = "Admin")]
4.3.2 Endpoint Details

POST /api/BugReports — TODO

Auth: [Authorize]

Rate limit: BugsWrite

Body DTO: BugReportCreateRequest — DTOs/Bugs/BugReportCreateRequest.cs:L3

Responses: 201 BugReportDto; 400 ProblemDetails

Code: WebAPI/Controllers/BugReportsController.cs:L7

GET /api/BugReports/{id:guid} — TODO

Auth: [Authorize]

Rate limit: ReadsLight

Path: id: Guid

Responses: 200 BugReportDto; 404 ProblemDetails

Code: WebAPI/Controllers/BugReportsController.cs:L48

GET /api/BugReports/my — TODO

Auth: [Authorize]

Rate limit: ReadsLight

Query: page?: int, size?: int

Responses: 200 PagedResult<BugReportDto>

Code: WebAPI/Controllers/BugReportsController.cs:L67

GET /api/BugReports — TODO

Auth: [Authorize(Roles="Admin")]

Rate limit: DashboardRead

Query: page?: int, size?: int

Responses: 200 PagedResult<BugReportDto>

Code: WebAPI/Controllers/BugReportsController.cs:L91

GET /api/BugReports/status/{status} — TODO

Auth: [Authorize(Roles="Admin")]

Rate limit: DashboardRead

Path: status: string

Query: page?: int, size?: int

Responses: 200 PagedResult<BugReportDto>; 400 ProblemDetails

Code: WebAPI/Controllers/BugReportsController.cs:L113

PATCH /api/BugReports/{id:guid}/status — TODO

Auth: [Authorize(Roles="Admin")]

Rate limit: ReadsLight

Path: id: Guid

Body DTO: BugReportStatusPatchRequest — DTOs/Bugs/BugReportStatusPatchRequest.cs:L3

Responses: 200 BugReportDto; 400/404 ProblemDetails

Code: WebAPI/Controllers/BugReportsController.cs:L136

4.4 ClubsController — Overview

TODO. Tham khảo WebAPI/Controllers/ClubsController.cs.

4.4.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
GET	/api/communities/{communityId:guid}/clubs	TODO	[Authorize]
POST	/api/Clubs	TODO	[Authorize]
GET	/api/Clubs/{id:guid}	TODO	[Authorize]
PATCH	/api/Clubs/{id:guid}	TODO	[Authorize]
DELETE	/api/Clubs/{id:guid}	TODO	[Authorize]
4.4.2 Endpoint Details

GET /api/communities/{communityId:guid}/clubs — TODO

Auth: [Authorize]

Rate limit: ClubsRead

Path: communityId: Guid

Query: name?: string, isPublic?: bool, membersFrom?: int, membersTo?: int, cursor?: string, size: int=20

Responses: 200 CursorPageResult<ClubBriefDto>; 400/401/429 ProblemDetails

Code: WebAPI/Controllers/ClubsController.cs:L10

POST /api/Clubs — TODO

Auth: [Authorize]

Rate limit: ClubsWrite

Body DTO: ClubCreateRequestDto — DTOs/Clubs/ClubCreateRequestDto.cs:L7

Responses: 201 Guid; 400/401/429 ProblemDetails

Code: WebAPI/Controllers/ClubsController.cs:L94

GET /api/Clubs/{id:guid} — TODO

Auth: [Authorize]

Rate limit: ClubsRead

Path: id: Guid

Responses: 200 ClubDetailDto; 401/404 ProblemDetails

Code: WebAPI/Controllers/ClubsController.cs:L128

PUT /api/Clubs/{id:guid} — TODO

Auth: [Authorize]

Rate limit: ClubsWrite

Path: id: Guid

Body DTO: ClubUpdateRequestDto — DTOs/Clubs/ClubUpdateRequestDto.cs:L6

Responses: 204; 400/401/403/404 ProblemDetails

Code: WebAPI/Controllers/ClubsController.cs:L145

DELETE /api/Clubs/{id:guid} — TODO

Auth: [Authorize]

Rate limit: ClubsWrite

Path: id: Guid

Responses: 204; 401/403/404 ProblemDetails

Code: WebAPI/Controllers/ClubsController.cs:L169

4.5 CommunitiesController — Overview

TODO. Tham khảo WebAPI/Controllers/CommunitiesController.cs.

4.5.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
POST	/api/Communities	TODO	[Authorize]
GET	/api/Communities	TODO	[Authorize]
GET	/api/Communities/{id:guid}	TODO	[Authorize]
PATCH	/api/Communities/{id:guid}	TODO	[Authorize]
DELETE	/api/Communities/{id:guid}	TODO	[Authorize]
GET	/api/Communities/discover	TODO	AllowAnonymous
4.5.2 Endpoint Details

POST /api/Communities — TODO

Auth: [Authorize]

Rate limit: CommunitiesWrite

Body DTO: CommunityCreateRequestDto — DTOs/Communities/CommunityCreateRequestDto.cs:L11

Responses: 201 object; 400/401/429 ProblemDetails

Code: WebAPI/Controllers/CommunitiesController.cs:L10

GET /api/Communities — TODO

Auth: [Authorize]

Rate limit: CommunitiesRead

Query: school?: string, gameId?: Guid, isPublic?: bool, membersFrom?: int, membersTo?: int, cursor?: string, size: int=20

Responses: 200 CursorPageResult<CommunityBriefDto>; 400/401/429 ProblemDetails

Code: WebAPI/Controllers/CommunitiesController.cs:L75

GET /api/Communities/{id:guid} — TODO

Auth: [Authorize]

Rate limit: CommunitiesRead

Path: id: Guid

Responses: 200 CommunityDetailDto; 401/404 ProblemDetails

Code: WebAPI/Controllers/CommunitiesController.cs:L123

PUT /api/Communities/{id:guid} — TODO

Auth: [Authorize]

Rate limit: CommunitiesWrite

Path: id: Guid

Body DTO: CommunityUpdateRequestDto — DTOs/Communities/CommunityUpdateRequestDto.cs:L10

Responses: 204; 400/401/404/429 ProblemDetails

Code: WebAPI/Controllers/CommunitiesController.cs:L145

DELETE /api/Communities/{id:guid} — TODO

Auth: [Authorize]

Rate limit: CommunitiesWrite

Path: id: Guid

Responses: 204; 401/403/404/409/429 ProblemDetails

Code: WebAPI/Controllers/CommunitiesController.cs:L172

GET /api/Communities/discover — TODO

Auth: AllowAnonymous

Rate limit: CommunitiesRead

Query: query?: string, offset?: int, limit?: int, orderBy?: string

Responses: 200 PagedResult<CommunityDetailDto>; 400/429 ProblemDetails

Code: WebAPI/Controllers/CommunitiesController.cs:L208

4.6 DashboardController — Overview

TODO. Tham khảo WebAPI/Controllers/DashboardController.cs.

4.6.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
GET	/api/Dashboard/today	TODO	[Authorize]
4.6.2 Endpoint Details

GET /api/Dashboard/today — TODO

Auth: [Authorize]

Rate limit: DashboardRead

Responses: 200 DashboardTodayDto; 401/429/500 ProblemDetails

Code: WebAPI/Controllers/DashboardController.cs:L12

4.7 EventsController — Overview

TODO. Tham khảo WebAPI/Controllers/EventsController.cs.

4.7.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
POST	/api/Events	TODO	[Authorize]
POST	/api/Events/{eventId:guid}/open	TODO	[Authorize]
POST	/api/Events/{eventId:guid}/cancel	TODO	[Authorize]
GET	/api/Events/{eventId:guid}	TODO	[Authorize]
GET	/api/Events	TODO	[Authorize]
GET	/api/organizer/events	TODO	[Authorize]
4.7.2 Endpoint Details

POST /api/Events — TODO

Auth: [Authorize]

Rate limit: EventsWrite

Body DTO: EventCreateRequestDto — DTOs/Events/EventCreateRequestDto.cs:L3

Responses: 201 object; 400/401/403/404/409/429/500 ProblemDetails

Code: WebAPI/Controllers/EventsController.cs:L5

POST /api/Events/{eventId:guid}/open — TODO

Auth: [Authorize]

Rate limit: EventsWrite

Path: eventId: Guid

Responses: 204; 403 object; 400/401/404/409/429/500 ProblemDetails

Code: WebAPI/Controllers/EventsController.cs:L57

POST /api/Events/{eventId:guid}/cancel — TODO

Auth: [Authorize]

Rate limit: EventsWrite

Path: eventId: Guid

Responses: 204; 400/401/403/404/409/429/500 ProblemDetails

Code: WebAPI/Controllers/EventsController.cs:L85

GET /api/Events/{eventId:guid} — TODO

Auth: [Authorize]

Rate limit: ReadsLight

Path: eventId: Guid

Responses: 200 EventDetailDto; 401/403/404/429/500 ProblemDetails

Code: WebAPI/Controllers/EventsController.cs:L107

GET /api/Events — TODO

Auth: [Authorize]

Rate limit: ReadsHeavy

Query:
statuses?: IEnumerable<EventStatus>, communityId?: Guid, organizerId?: Guid,
from?: DateTimeOffset, to?: DateTimeOffset, search?: string, sort?: string,
page: int=1, pageSize: int=PaginationOptions.DefaultPageSize

Responses: 200 PagedResponse<EventDetailDto>; 401/403/404/429/500 ProblemDetails

Code: WebAPI/Controllers/EventsController.cs:L127

GET /api/organizer/events — TODO

Auth: [Authorize]

Rate limit: ReadsHeavy

Query:
statuses?: IEnumerable<EventStatus>, communityId?: Guid,
from?: DateTimeOffset, to?: DateTimeOffset, search?: string, sort?: string,
page: int=1, pageSize: int=PaginationOptions.DefaultPageSize

Responses: 200 PagedResponse<EventDetailDto>; 401/403/404/429/500 ProblemDetails

Code: WebAPI/Controllers/EventsController.cs:L180

4.8 FriendsController — Overview

TODO. Tham khảo WebAPI/Controllers/FriendsController.cs.

4.8.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
POST	/api/Friends/{userId:guid}/invite	TODO	[Authorize]
POST	/api/Friends/{userId:guid}/accept	TODO	[Authorize]
POST	/api/Friends/{userId:guid}/decline	TODO	[Authorize]
POST	/api/Friends/{userId:guid}/cancel	TODO	[Authorize]
GET	/api/Friends	TODO	[Authorize]
4.8.2 Endpoint Details

POST /api/Friends/{userId:guid}/invite — TODO

Auth: [Authorize]

Rate limit: FriendInvite

Path: userId: Guid

Responses: 204; 400/401/403/404/409/429/500 ProblemDetails

Code: WebAPI/Controllers/FriendsController.cs:L8

POST /api/Friends/{userId:guid}/accept — TODO

Auth: [Authorize]

Rate limit: FriendAction

Path: userId: Guid

Responses: 204; 400/401/403/404/409/429/500 ProblemDetails

Code: WebAPI/Controllers/FriendsController.cs:L42

POST /api/Friends/{userId:guid}/decline — TODO

Auth: [Authorize]

Rate limit: FriendAction

Path: userId: Guid

Responses: 204; 400/401/403/404/409/429/500 ProblemDetails

Code: WebAPI/Controllers/FriendsController.cs:L65

POST /api/Friends/{userId:guid}/cancel — TODO

Auth: [Authorize]

Rate limit: FriendAction

Path: userId: Guid

Responses: 204; 400/401/403/404/409/429/500 ProblemDetails

Code: WebAPI/Controllers/FriendsController.cs:L88

GET /api/Friends — TODO

Auth: [Authorize]

Query: filter?: string, request: CursorRequest

Responses: 200 CursorPageResult<FriendDto>; 400/401/403/404/409/429/500 ProblemDetails

Code: WebAPI/Controllers/FriendsController.cs:L111

4.9 GoogleAuthController — Overview

TODO. Tham khảo WebAPI/Controllers/GoogleAuthController.cs.

4.9.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
POST	/api/GoogleAuth/login	TODO	AllowAnonymous
4.9.2 Endpoint Details

POST /api/GoogleAuth/login — TODO

Auth: AllowAnonymous

Body DTO: GoogleLoginRequest — DTOs/Auth/Requests/GoogleLoginRequest.cs:L5

Responses: 200/4xx/5xx (TODO chi tiết)

Code: WebAPI/Controllers/GoogleAuthController.cs:L5

4.10 PaymentsController — Overview

TODO. Tham khảo WebAPI/Controllers/PaymentsController.cs.

4.10.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
POST	/api/Payments/{intentId:guid}/confirm	TODO	[Authorize]
GET	/api/Payments/{intentId:guid}	TODO	[Authorize]
POST	/api/Payments/{intentId:guid}/vnpay/checkout	TODO	[Authorize]
POST	/api/Payments/webhooks/vnpay	TODO	AllowAnonymous
GET	/api/Payments/vnpay/return	TODO	AllowAnonymous
4.10.2 Endpoint Details

POST /api/Payments/{intentId:guid}/confirm — TODO

Auth: [Authorize]

Rate limit: PaymentsWrite

Path: intentId: Guid

Responses: 204; 400/401/403/404/409/429/500 ProblemDetails

Code: WebAPI/Controllers/PaymentsController.cs:L6

GET /api/Payments/{intentId:guid} — TODO

Auth: [Authorize]

Rate limit: ReadsLight

Path: intentId: Guid

Responses: 200 PaymentIntentDto; 401/403/404/429/500 ProblemDetails

Code: WebAPI/Controllers/PaymentsController.cs:L42

POST /api/Payments/{intentId:guid}/vnpay/checkout — TODO

Auth: [Authorize]

Rate limit: PaymentsWrite

Path: intentId: Guid

Body DTO: VnPayCheckoutRequest? (chưa tìm thấy định nghĩa)

Responses: 200 string; 400/401/403/404/409/429/500 ProblemDetails

Code: WebAPI/Controllers/PaymentsController.cs:L62

POST /api/Payments/webhooks/vnpay — TODO

Auth: AllowAnonymous

Rate limit: PaymentsWebhook

Responses: 200 VnPayWebhookResponse

Code: WebAPI/Controllers/PaymentsController.cs:L87

GET /api/Payments/vnpay/return — TODO

Auth: AllowAnonymous

Responses: 302; 400

Code: WebAPI/Controllers/PaymentsController.cs:L113

4.11 QuestsController — Overview

TODO. Tham khảo WebAPI/Controllers/QuestsController.cs.

4.11.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
GET	/api/Quests/today	TODO	[Authorize]
POST	/api/Quests/check-in	TODO	[Authorize]
POST	/api/Quests/join-room/{roomId:guid}	TODO	[Authorize]
POST	/api/Quests/attend-event/{eventId:guid}	TODO	[Authorize]
4.11.2 Endpoint Details

GET /api/Quests/today — TODO

Auth: [Authorize]

Responses: 200 QuestTodayDto; 401/500 ProblemDetails

Code: WebAPI/Controllers/QuestsController.cs:L15

POST /api/Quests/check-in — TODO

Auth: [Authorize]

Responses: 204; 400/401/500 ProblemDetails

Code: WebAPI/Controllers/QuestsController.cs:L51

POST /api/Quests/join-room/{roomId:guid} — TODO

Auth: [Authorize]

Path: roomId: Guid

Responses: 204; 400/401/404/500 ProblemDetails

Code: WebAPI/Controllers/QuestsController.cs:L73

POST /api/Quests/attend-event/{eventId:guid} — TODO

Auth: [Authorize]

Path: eventId: Guid

Responses: 204; 400/401/404/500 ProblemDetails

Code: WebAPI/Controllers/QuestsController.cs:L96

4.12 RegistrationsController — Overview

TODO. Tham khảo WebAPI/Controllers/RegistrationsController.cs.

4.12.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
POST	/api/events/{eventId:guid}/registrations	TODO	[Authorize]
GET	/api/events/{eventId:guid}/registrations	TODO	[Authorize]
GET	/api/me/registrations	TODO	[Authorize]
4.12.2 Endpoint Details

POST /api/events/{eventId:guid}/registrations — TODO

Auth: [Authorize]

Rate limit: RegistrationsWrite

Path: eventId: Guid

Responses: 201 object; 400/401/403/404/409/429/500 ProblemDetails

Code: WebAPI/Controllers/RegistrationsController.cs:L7

GET /api/events/{eventId:guid}/registrations — TODO

Auth: [Authorize]

Rate limit: ReadsHeavy

Path: eventId: Guid

Query: statuses?: IEnumerable<EventRegistrationStatus>, page: int=1, pageSize: int=PaginationOptions.DefaultPageSize

Responses: 200 PagedResponse<RegistrationListItemDto>; 401/403/404/429/500 ProblemDetails

Code: WebAPI/Controllers/RegistrationsController.cs:L56

GET /api/me/registrations — TODO

Auth: [Authorize]

Rate limit: ReadsLight

Query: statuses?: IEnumerable<EventRegistrationStatus>, page: int=1, pageSize: int=PaginationOptions.DefaultPageSize

Responses: 200 PagedResponse<MyRegistrationDto>; 401/403/404/429/500 ProblemDetails

Code: WebAPI/Controllers/RegistrationsController.cs:L89

4.13 RolesController — Overview

TODO. Tham khảo WebAPI/Controllers/RolesController.cs.

4.13.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
GET	/api/Roles	TODO	No
GET	/api/Roles/exists	TODO	No
POST	/api/Roles	TODO	No
PUT	/api/Roles/{id:guid}	TODO	No
DELETE	/api/Roles/{id:guid}	TODO	No
POST	/api/Roles/{id:guid}/soft-delete	TODO	No
POST	/api/Roles/{id:guid}/restore	TODO	No
POST	/api/Roles/batch-create	TODO	No
PUT	/api/Roles/batch-update	TODO	No
DELETE	/api/Roles/batch-delete	TODO	No
POST	/api/Roles/batch-soft-delete	TODO	No
POST	/api/Roles/batch-restore	TODO	No
4.13.2 Endpoint Details

GET /api/Roles — TODO

Query: paging: PageRequest, filter: RoleFilter

Responses: 200 PagedResult<RoleDto>

Code: WebAPI/Controllers/RolesController.cs:L3

GET /api/Roles/exists — TODO

Query: name: string (required), excludeId?: Guid

Responses: 200 bool

Code: WebAPI/Controllers/RolesController.cs:L35

POST /api/Roles — TODO

Body DTO: CreateRoleRequest — DTOs/Roles/Requests/RoleRequests.cs:L3

Responses: 201 RoleDto; 400/409

Code: WebAPI/Controllers/RolesController.cs:L46

PUT /api/Roles/{id:guid} — TODO

Path: id: Guid

Body DTO: UpdateRoleRequest — DTOs/Roles/Requests/RoleRequests.cs:L8

Responses: 200 RoleDto; 400/404/409

Code: WebAPI/Controllers/RolesController.cs:L62

DELETE /api/Roles/{id:guid} — TODO

Path: id: Guid

Responses: 204; 404

Code: WebAPI/Controllers/RolesController.cs:L74

POST /api/Roles/{id:guid}/soft-delete — TODO

Path: id: Guid

Responses: 204; 404

Code: WebAPI/Controllers/RolesController.cs:L84

POST /api/Roles/{id:guid}/restore — TODO

Path: id: Guid

Responses: 204; 404

Code: WebAPI/Controllers/RolesController.cs:L94

POST /api/Roles/batch-create — TODO

Body: IEnumerable<CreateRoleRequest> (chưa tìm thấy định nghĩa)

Responses: 200 BatchResult<Guid, RoleDto>; 400/409

Code: WebAPI/Controllers/RolesController.cs:L106

PUT /api/Roles/batch-update — TODO

Body: (chưa tìm thấy định nghĩa)

Responses: 200 BatchResult<Guid, RoleDto>; 400/409

Code: WebAPI/Controllers/RolesController.cs:L117

DELETE /api/Roles/batch-delete — TODO

Body: IEnumerable<Guid>

Responses: 200 BatchOutcome<Guid>

Code: WebAPI/Controllers/RolesController.cs:L128

POST /api/Roles/batch-soft-delete — TODO

Body: IEnumerable<Guid>

Responses: 200 BatchOutcome<Guid>

Code: WebAPI/Controllers/RolesController.cs:L137

POST /api/Roles/batch-restore — TODO

Body: IEnumerable<Guid>

Responses: 200 BatchOutcome<Guid>

Code: WebAPI/Controllers/RolesController.cs:L146

4.14 RoomsController — Overview

TODO. Tham khảo WebAPI/Controllers/RoomsController.cs.

4.14.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
GET	/api/Rooms/{id:guid}	TODO	[Authorize]
GET	/api/Rooms/{id:guid}/members	TODO	[Authorize]
POST	/api/Rooms	TODO	[Authorize]
POST	/api/Rooms/{id:guid}/join	TODO	[Authorize]
POST	/api/Rooms/{id:guid}/approve/{userId:guid}	TODO	[Authorize]
POST	/api/Rooms/{id:guid}/leave	TODO	[Authorize]
POST	/api/Rooms/{id:guid}/kickban/{userId:guid}	TODO	[Authorize]
PATCH	/api/Rooms/{id:guid}	TODO	[Authorize]
POST	/api/Rooms/{id:guid}/transfer-ownership/{newOwnerId:guid}	TODO	[Authorize]
DELETE	/api/Rooms/{id:guid}	TODO	[Authorize]
4.14.2 Endpoint Details

GET /api/Rooms/{id:guid} — TODO

Auth: [Authorize]

Rate limit: RoomsRead

Path: id: Guid

Responses: 200 RoomDetailDto; 404/429 ProblemDetails

Code: WebAPI/Controllers/RoomsController.cs:L10

GET /api/Rooms/{id:guid}/members — TODO

Auth: [Authorize]

Rate limit: RoomsRead

Path: id: Guid

Query: skip: int=0, take: int=20

Responses: 200 IReadOnlyList<RoomMemberBriefDto>; 400/404/429 ProblemDetails

Code: WebAPI/Controllers/RoomsController.cs:L54

POST /api/Rooms — TODO

Auth: [Authorize]

Rate limit: RoomsCreate

Body DTO: RoomCreateRequestDto — DTOs/Rooms/RoomCreateRequestDto.cs:L6

Responses: 201 Guid; 400/401/404/429 ProblemDetails

Code: WebAPI/Controllers/RoomsController.cs:L83

POST /api/Rooms/{id:guid}/join — TODO

Auth: [Authorize]

Rate limit: RoomsWrite

Path: id: Guid

Body DTO: RoomJoinRequestDto — DTOs/Rooms/RoomJoinRequestDto.cs:L6

Responses: 204; 400/401/403/404/409/429 ProblemDetails

Code: WebAPI/Controllers/RoomsController.cs:L135

POST /api/Rooms/{id:guid}/approve/{userId:guid} — TODO

Auth: [Authorize]

Rate limit: RoomsWrite

Path: id: Guid, userId: Guid

Responses: 204; 401/403/404/409/429 ProblemDetails

Code: WebAPI/Controllers/RoomsController.cs:L177

POST /api/Rooms/{id:guid}/leave — TODO

Auth: [Authorize]

Rate limit: RoomsWrite

Path: id: Guid

Responses: 204; 401/403/404/409/429 ProblemDetails

Code: WebAPI/Controllers/RoomsController.cs:L217

POST /api/Rooms/{id:guid}/kickban/{userId:guid} — TODO

Auth: [Authorize]

Rate limit: RoomsWrite

Path: id: Guid, userId: Guid

Query: ban: bool=false

Responses: 204; 401/403/404/409/429 ProblemDetails

Code: WebAPI/Controllers/RoomsController.cs:L256

PUT /api/Rooms/{id:guid} — TODO

Auth: [Authorize]

Rate limit: RoomsWrite

Path: id: Guid

Body DTO: RoomUpdateRequestDto — DTOs/Rooms/RoomUpdateRequestDto.cs:L6

Responses: 204; 400/401/403/404/409/429 ProblemDetails

Code: WebAPI/Controllers/RoomsController.cs:L298

POST /api/Rooms/{id:guid}/transfer-ownership/{newOwnerId:guid} — TODO

Auth: [Authorize]

Rate limit: RoomsWrite

Path: id: Guid, newOwnerId: Guid

Responses: 204; 401/403/404/409/429 ProblemDetails

Code: WebAPI/Controllers/RoomsController.cs:L334

DELETE /api/Rooms/{id:guid} — TODO

Auth: [Authorize]

Rate limit: RoomsArchive

Path: id: Guid

Responses: 204; 401/403/404/409/429 ProblemDetails

Code: WebAPI/Controllers/RoomsController.cs:L367

4.15 TeammatesController — Overview

TODO. Tham khảo WebAPI/Controllers/TeammatesController.cs.

4.15.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
GET	/api/Teammates	TODO	[Authorize]
4.15.2 Endpoint Details

GET /api/Teammates — TODO

Auth: [Authorize]

Rate limit: TeammatesRead

Query: gameId?: Guid, university?: string, skill?: GameSkillLevel, onlineOnly: bool=false, cursor?: string, size: int=20

Responses: 200 CursorPageResult<TeammateDto>; 400/401/429/500 ProblemDetails

Code: WebAPI/Controllers/TeammatesController.cs:L12

4.16 UsersController — Overview

TODO. Tham khảo WebAPI/Controllers/UsersController.cs.

4.16.1 Endpoint Catalog
Method	Path	Mô tả ngắn	Auth?
GET	/api/Users	TODO	[Authorize(Roles = "Admin")]
GET	/api/Users/{id:guid}	TODO	[Authorize(Roles = "Admin")]
POST	/api/Users	TODO	[Authorize(Roles = "Admin")]
PUT	/api/Users/{id:guid}	TODO	[Authorize(Roles = "Admin")]
PATCH	/api/Users/{id:guid}/lockout	TODO	[Authorize(Roles = "Admin")]
POST	/api/Users/{id:guid}/roles:replace	TODO	[Authorize(Roles = "Admin")]
POST	/api/Users/{id:guid}/roles:modify	TODO	[Authorize(Roles = "Admin")]
POST	/api/Users/{id:guid}/password:change	TODO	[Authorize(Roles = "Admin")]
POST	/api/Users/password:send-reset	TODO	AllowAnonymous
POST	/api/Users/password:reset	TODO	AllowAnonymous
POST	/api/Users/{id:guid}/email:send-confirm	TODO	[Authorize(Roles = "Admin")]
POST	/api/Users/email:confirm	TODO	AllowAnonymous
POST	/api/Users/{id:guid}/email:send-change	TODO	[Authorize(Roles = "Admin")]
POST	/api/Users/email:confirm-change	TODO	AllowAnonymous
4.16.2 Endpoint Details

GET /api/Users — TODO

Auth: [Authorize(Roles="Admin")]

Query: filter: UserFilter, page: PageRequest

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/UsersController.cs:L5

GET /api/Users/{id:guid} — TODO

Auth: [Authorize(Roles="Admin")]

Path: id: Guid

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/UsersController.cs:L23

POST /api/Users — TODO

Auth: [Authorize(Roles="Admin")]

Body DTO: CreateUserAdminRequest — DTOs/Users/Requests/UserRequest.cs:L6

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/UsersController.cs:L32

PUT /api/Users/{id:guid} — TODO

Auth: [Authorize(Roles="Admin")]

Path: id: Guid

Body DTO: UpdateUserRequest — DTOs/Users/Requests/UserRequest.cs:L32

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/UsersController.cs:L41

PATCH /api/Users/{id:guid}/lockout — TODO

Auth: [Authorize(Roles="Admin")]

Path: id: Guid

Body DTO: SetLockoutRequest — DTOs/Users/Requests/UserRequest.cs:L69

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/UsersController.cs:L50

POST /api/Users/{id:guid}/roles:replace — TODO

Auth: [Authorize(Roles="Admin")]

Path: id: Guid

Body DTO: ReplaceRolesRequest — DTOs/Users/Requests/UserRequest.cs:L90

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/UsersController.cs:L59

POST /api/Users/{id:guid}/roles:modify — TODO

Auth: [Authorize(Roles="Admin")]

Path: id: Guid

Body DTO: ModifyRolesRequest — DTOs/Users/Requests/UserRequest.cs:L87

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/UsersController.cs:L67

POST /api/Users/{id:guid}/password:change — TODO

Auth: [Authorize(Roles="Admin")]

Path: id: Guid

Body DTO: ChangePasswordRequest — DTOs/Users/Requests/UserRequest.cs:L54

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/UsersController.cs:L76

POST /api/Users/password:send-reset — TODO

Auth: AllowAnonymous

Query: callbackBaseUrl: string (required)

Body DTO: ForgotPasswordRequest — DTOs/Users/Requests/UserRequest.cs:L59

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/UsersController.cs:L84

POST /api/Users/password:reset — TODO

Auth: AllowAnonymous

Body DTO: ResetPasswordRequest — DTOs/Users/Requests/UserRequest.cs:L63

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/UsersController.cs:L93

POST /api/Users/{id:guid}/email:send-confirm — TODO

Auth: [Authorize(Roles="Admin")]

Path: id: Guid

Query: callbackBaseUrl: string (required)

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/UsersController.cs:L103

POST /api/Users/email:confirm — TODO

Auth: AllowAnonymous

Body DTO: ConfirmEmailRequest — DTOs/Users/Requests/UserRequest.cs:L71

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/UsersController.cs:L111

POST /api/Users/{id:guid}/email:send-change — TODO

Auth: [Authorize(Roles="Admin")]

Path: id: Guid

Query: callbackBaseUrl: string (required)

Body DTO: ChangeEmailRequest — DTOs/Users/Requests/UserRequest.cs:L76

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/UsersController.cs:L121

POST /api/Users/email:confirm-change — TODO

Auth: AllowAnonymous

Body DTO: ConfirmChangeEmailRequest — DTOs/Users/Requests/UserRequest.cs:L81

Responses: 200/4xx/5xx (TODO)

Code: WebAPI/Controllers/UsersController.cs:L129

5. SignalR

TODO: Kiểm tra WebAPI/Hubs (PresenceHub, ChatHub) → ghi rõ URL, phương thức, payload.

6. Rate limit & Policies
Policy	Giới hạn	Áp dụng
EventsWrite	60 req/phút/người	TODO: liệt kê endpoint Events write
RegistrationsWrite	60 req/phút/người	TODO
PaymentsWrite	120 req/phút/người	TODO
PaymentsWebhook	300 req/phút/IP/phút	TODO
ReadsHeavy	300 req/phút/người	TODO
ReadsLight	600 req/phút/người	TODO
FriendInvite	20 req/ngày/người	TODO (Friends invites)
FriendAction	60 req/phút/người	TODO
DashboardRead	120 req/phút/người	TODO
TeammatesRead	120 req/phút/người	GET /api/Teammates
RoomsCreate	10 req/ngày/người	TODO
RoomsRead	120 req/phút/người	TODO
RoomsWrite	30 req/phút/người	TODO
RoomsArchive	10 req/ngày/người	TODO
CommunitiesRead	120 req/phút/người	TODO
CommunitiesWrite	10 req/ngày/người	TODO
ClubsRead	120 req/phút/người	ClubsController đọc
ClubsWrite	10 req/ngày/người	ClubsController ghi
BugsWrite	20 req/ngày/người	Abuse/BugReports POST
7. Phụ lục

TODO: Liệt kê các enum chính (vd. Gender, …) và mô tả.

TODO: Thông số phân trang mặc định cho PageRequest / CursorRequest.

TODO: Bảng mã lỗi chuẩn / mapping Error.Codes.
