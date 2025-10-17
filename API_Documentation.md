# Student Gamer Hub - API Documentation

## Overview
Student Gamer Hub is a platform for gaming communities, providing features like user management, friend systems, game catalogs, events, rooms, payments, and more.

**Base URL**: `/api`
**Authentication**: JWT Bearer Token (for most endpoints)
**Rate Limiting**: Various limits applied per endpoint

---

## ?? Authentication & User Management

### AuthController (`/api/auth`)

#### User Registration & Authentication
- **POST** `/user-register` - Register new user account
  - Body: `RegisterRequest`
  - Returns: `201 Created` with user info (Id, UserName, Email, FullName, EmailConfirmed)

- **POST** `/login` - User login
  - Body: `LoginRequest`
  - Returns: `200 OK` with `AccessTokenResponse`
  - Sets HttpOnly refresh cookie and CSRF token

- **POST** `/refresh` - Refresh access token
  - Body: `RefreshTokenRequest` (optional if using cookies)
  - Requires CSRF validation
  - Returns: `200 OK` with new `AccessTokenResponse`

- **POST** `/revoke` - Revoke/logout
  - Body: `RevokeTokenRequest` (optional if using cookies)
  - Returns: `200 OK`
  - Clears auth cookies

#### Email Confirmation
- **POST** `/email:confirm` - Confirm email with token (Public)
  - Body: `ConfirmEmailRequest`
  - Returns: `200 OK` or error

#### Password Reset (Public)
- **POST** `/password:send-reset` - Send password reset email
  - Body: `ForgotPasswordRequest`
  - Query: `callbackBaseUrl` (optional)
  - Returns: `200 OK` or error

- **POST** `/password:reset` - Reset password with token
  - Body: `ResetPasswordRequest`
  - Returns: `200 OK` or error

#### Self-Service ("me" endpoints)
- **GET** `/me` - Get current user profile
  - Returns: `200 OK` with user details

- **PUT** `/me` - Update current user profile
  - Body: `UpdateUserSelfRequest`
  - Returns: `200 OK` with updated user

- **POST** `/me/password:change` - Change own password
  - Body: `ChangePasswordRequest`
  - Returns: `200 OK` or error

- **POST** `/me/email:send-confirm` - Resend email confirmation
  - Query: `callbackBaseUrl`
  - Returns: `200 OK` or error

- **POST** `/me/email:send-change` - Send email change confirmation
  - Body: `ChangeEmailRequest`
  - Query: `callbackBaseUrl`
  - Returns: `200 OK` or error

### UsersController (`/api/users`) - Admin Only
*Requires Admin role*

#### User Management
- **GET** `/` - Search users with pagination
  - Query: `UserFilter`, `PageRequest`
  - Returns: Paginated user list

- **GET** `/{id}` - Get user by ID
  - Returns: User details

- **POST** `/` - Create new user (Admin)
  - Body: `CreateUserAdminRequest`
  - Returns: `201 Created` with location header

- **PUT** `/{id}` - Update user
  - Body: `UpdateUserRequest`
  - Returns: `200 OK` or error

#### User Status Management
- **PATCH** `/{id}/lockout` - Enable/disable user lockout
  - Body: `SetLockoutRequest`
  - Returns: `200 OK` or error

#### Role Management
- **POST** `/{id}/roles:replace` - Replace user's roles
  - Body: `ReplaceRolesRequest`
  - Returns: `200 OK` or error

- **POST** `/{id}/roles:modify` - Add/remove roles incrementally
  - Body: `ModifyRolesRequest`
  - Returns: `200 OK` or error

#### Admin Password & Email Operations
- **POST** `/{id}/password:change` - Admin change user password
- **POST** `/{id}/email:send-confirm` - Admin send email confirmation
- **POST** `/{id}/email:send-change` - Admin send email change confirmation

### GoogleAuthController (`/api/googleauth`)

- **POST** `/login` - Google OAuth login
  - Body: `GoogleLoginRequest`
  - Returns: `200 OK` with access token
  - Sets refresh cookie and CSRF token

---

## ?? Social Features

### FriendsController (`/api/friends`)
*Requires Authentication*

#### Friend Management
- **POST** `/{userId}/invite` - Send friend invitation
  - Rate Limit: FriendInvite
  - Returns: `204 No Content` or error

- **POST** `/{userId}/accept` - Accept friend invitation
  - Rate Limit: FriendAction
  - Returns: `204 No Content` or error

- **POST** `/{userId}/decline` - Decline friend invitation
  - Rate Limit: FriendAction
  - Returns: `204 No Content` or error

- **POST** `/{userId}/cancel` - Cancel sent invitation
  - Rate Limit: FriendAction
  - Returns: `204 No Content` or error

#### Friend Lists
- **GET** `/` - List friends with filtering
  - Query: `filter` (All/Pending/Confirmed), `CursorRequest`
  - Returns: `200 OK` with `CursorPageResult<FriendDto>`

### TeammatesController (`/api/teammates`)
*Requires Authentication*

- **GET** `/` - Search for potential teammates
  - Query: `gameId`, `university`, `skill`, `onlineOnly`, `cursor`, `size`
  - Rate Limit: TeammatesRead
  - Returns: `200 OK` with `CursorPageResult<TeammateDto>`
  - Sorted by: online status, points, shared games, user ID (all DESC)

---

## ?? Gaming Features

### GamesController (`/api/games`)

#### Game Catalog Management
- **POST** `/` - Create new game entry
  - Body: `GameCreateRequestDto`
  - Rate Limit: GamesWrite
  - Returns: `201 Created` with game ID

- **PATCH** `/{id}/rename` - Rename existing game
  - Body: `GameRenameRequestDto`
  - Rate Limit: GamesWrite
  - Returns: `204 No Content` or error

- **DELETE** `/{id}` - Soft-delete game
  - Rate Limit: GamesWrite
  - Returns: `204 No Content` or error

#### Game Search
- **GET** `/` - Search games with cursor pagination (Public)
  - Query: `q` (search), `cursor`, `direction`, `size`, `sort`, `desc`
  - Rate Limit: GamesRead
  - Returns: `200 OK` with `CursorPageResult<GameDto>`

### UserGamesController (`/api/me/games`)
*User's Personal Game Catalog*

- **GET** `/` - List user's games
  - Rate Limit: GamesRead
  - Returns: `200 OK` with `IReadOnlyList<UserGameDto>`

- **POST** `/{gameId}` - Add game to user's catalog
  - Body: `UserGameUpsertRequestDto` (InGameName, Skill)
  - Rate Limit: GamesWrite
  - Returns: `204 No Content` or error

- **PUT** `/{gameId}` - Update user-game association
  - Body: `UserGameUpsertRequestDto`
  - Rate Limit: GamesWrite
  - Returns: `204 No Content` or error

- **DELETE** `/{gameId}` - Remove game from user's catalog
  - Rate Limit: GamesWrite
  - Returns: `204 No Content` or error

---

## ??? Community & Club System

### CommunitiesController (`/api/communities`)

#### Community Management
- **POST** `/` - Create new community
  - Body: `CommunityCreateRequestDto`
  - Rate Limit: CommunitiesWrite
  - Returns: `201 Created` with community ID

- **GET** `/` - Search communities with filtering
  - Query: `school`, `gameId`, `isPublic`, `membersFrom`, `membersTo`, `cursor`, `size`
  - Rate Limit: CommunitiesRead
  - Returns: `200 OK` with `CursorPageResult<CommunityBriefDto>`

- **GET** `/{id}` - Get community details
  - Rate Limit: CommunitiesRead
  - Returns: `200 OK` with `CommunityDetailDto`

- **PATCH** `/{id}` - Update community metadata
  - Body: `CommunityUpdateRequestDto`
  - Rate Limit: CommunitiesWrite
  - Returns: `204 No Content` or error

- **DELETE** `/{id}` - Archive community (soft delete)
  - Rate Limit: CommunitiesWrite
  - Returns: `204 No Content` or error

#### Community Discovery
- **GET** `/discover` - Discover popular communities (Public)
  - Query: `school`, `gameId`, `cursor`, `size`
  - Rate Limit: CommunitiesRead
  - Returns: `200 OK` with `DiscoverResponse`
  - Sorted by popularity metrics

### ClubsController (`/api/clubs`)

#### Club Management
- **POST** `/` - Create new club
  - Body: `ClubCreateRequestDto`
  - Rate Limit: ClubsWrite (10 per day)
  - Returns: `201 Created` with club ID

- **GET** `/{id}` - Get club details
  - Rate Limit: ClubsRead
  - Returns: `200 OK` with `ClubDetailDto`

- **PATCH** `/{id}` - Update club information
  - Body: `ClubUpdateRequestDto`
  - Rate Limit: ClubsWrite
  - Returns: `204 No Content` or error

- **DELETE** `/{id}` - Archive club (soft delete)
  - Rate Limit: ClubsWrite
  - Returns: `204 No Content` or error

#### Club Search
- **GET** `/api/communities/{communityId}/clubs` - Search clubs in community
  - Query: `name`, `isPublic`, `membersFrom`, `membersTo`, `cursor`, `size`
  - Rate Limit: ClubsRead
  - Returns: `200 OK` with `CursorPageResult<ClubBriefDto>`

---

## ?? Room System

### RoomsController (`/api/rooms`)

#### Room Management
- **POST** `/` - Create new room
  - Body: `RoomCreateRequestDto`
  - Rate Limit: RoomsCreate (10 per day)
  - Returns: `201 Created` with room ID

- **GET** `/{id}` - Get room details
  - Rate Limit: RoomsRead
  - Returns: `200 OK` with `RoomDetailDto`

- **PATCH** `/{id}` - Update room metadata (owner only)
  - Body: `RoomUpdateRequestDto`
  - Rate Limit: RoomsWrite
  - Returns: `204 No Content` or error

- **DELETE** `/{id}` - Archive room (owner only)
  - Rate Limit: RoomsArchive (10 per day)
  - Returns: `204 No Content` or error

#### Room Membership
- **POST** `/{id}/join` - Join room
  - Body: `RoomJoinRequestDto` (password if required)
  - Rate Limit: RoomsWrite
  - Returns: `204 No Content` or error

- **POST** `/{id}/leave` - Leave room
  - Rate Limit: RoomsWrite
  - Returns: `204 No Content` or error

- **GET** `/{id}/members` - List room members
  - Query: `skip`, `take`
  - Rate Limit: RoomsRead
  - Returns: `200 OK` with `IReadOnlyList<RoomMemberBriefDto>`

#### Room Moderation
- **POST** `/{id}/approve/{userId}` - Approve pending member (Owner/Moderator)
  - Rate Limit: RoomsWrite
  - Returns: `204 No Content` or error

- **POST** `/{id}/kickban/{userId}` - Kick or ban member (Owner/Moderator)
  - Query: `ban` (boolean)
  - Rate Limit: RoomsWrite
  - Returns: `204 No Content` or error

- **POST** `/{id}/transfer-ownership/{newOwnerId}` - Transfer ownership
  - Rate Limit: RoomsWrite
  - Returns: `204 No Content` or error

---

## ?? Events & Registration

### EventsController (`/api/events`)

#### Event Management
- **POST** `/` - Create new event
  - Body: `EventCreateRequestDto`
  - Rate Limit: EventsWrite
  - Returns: `201 Created` with event ID

- **POST** `/{eventId}/open` - Open event for registration
  - Rate Limit: EventsWrite
  - Returns: `204 No Content` or `403 Forbidden` with top-up info

- **POST** `/{eventId}/cancel` - Cancel event
  - Rate Limit: EventsWrite
  - Returns: `204 No Content` or error

#### Event Discovery
- **GET** `/{eventId}` - Get event details
  - Rate Limit: ReadsLight
  - Returns: `200 OK` with `EventDetailDto`

- **GET** `/` - Search events
  - Query: `statuses`, `communityId`, `organizerId`, `from`, `to`, `search`, `sort`, `page`, `pageSize`
  - Rate Limit: ReadsHeavy
  - Returns: `200 OK` with `PagedResponse<EventDetailDto>`

- **GET** `/api/organizer/events` - List organizer's events
  - Query: Same as search
  - Rate Limit: ReadsHeavy
  - Returns: `200 OK` with `PagedResponse<EventDetailDto>`

### RegistrationsController (`/api/events/{eventId}/registrations`)

#### Event Registration
- **POST** `/` - Register for event
  - Rate Limit: RegistrationsWrite
  - Returns: `201 Created` with payment intent ID

- **GET** `/` - List event registrations (Organizer only)
  - Query: `statuses`, `page`, `pageSize`
  - Rate Limit: ReadsHeavy
  - Returns: `200 OK` with `PagedResponse<RegistrationListItemDto>`

- **GET** `/api/me/registrations` - List user's registrations
  - Query: `statuses`, `page`, `pageSize`
  - Rate Limit: ReadsLight
  - Returns: `200 OK` with `PagedResponse<MyRegistrationDto>`

---

## ?? Payment System

### PaymentsController (`/api/payments`)

#### Payment Processing
- **POST** `/{intentId}/confirm` - Confirm payment
  - Rate Limit: PaymentsWrite
  - Returns: `204 No Content` or error

- **GET** `/{intentId}` - Get payment intent details
  - Rate Limit: ReadsLight
  - Returns: `200 OK` with `PaymentIntentDto`

#### VNPay Integration
- **POST** `/{intentId}/vnpay/checkout` - Create VNPay checkout URL
  - Body: `VnPayCheckoutRequest` (ReturnUrl)
  - Rate Limit: PaymentsWrite
  - Returns: `200 OK` with payment URL

- **POST** `/webhooks/vnpay` - VNPay webhook (Public)
  - Rate Limit: PaymentsWebhook
  - Returns: `200 OK` with `VnPayWebhookResponse`

- **GET** `/vnpay/return` - VNPay return handler (Public)
  - Returns: `302 Found` redirect to SPA

---

## ?? Quest & Achievement System

### QuestsController (`/api/quests`)
*Daily quest system using Asia/Ho_Chi_Minh timezone*

- **GET** `/today` - Get today's quests and progress
  - Returns: `200 OK` with `QuestTodayDto`

- **POST** `/check-in` - Manual daily check-in (+5 points)
  - Returns: `204 No Content` or error
  - Idempotent: once per day

- **POST** `/join-room/{roomId}` - Mark room join quest (+5 points)
  - Returns: `204 No Content` or error
  - Idempotent: once per day

- **POST** `/attend-event/{eventId}` - Mark event attendance (+20 points)
  - Returns: `204 No Content` or error
  - Idempotent: once per day

---

## ?? Dashboard & Analytics

### DashboardController (`/api/dashboard`)

- **GET** `/today` - Get today's dashboard data
  - Rate Limit: DashboardRead (120 per minute)
  - Returns: `200 OK` with `DashboardTodayDto`
  - Includes: user points, quest status, today's events, activity metrics

---

## ?? Bug Reports & Abuse

### BugReportsController (`/api/bugreports`)

#### Bug Reporting
- **POST** `/` - Create bug report
  - Body: `BugReportCreateRequest`
  - Rate Limit: BugsWrite
  - Returns: `201 Created` with `BugReportDto`

- **GET** `/{id}` - Get bug report by ID
  - Rate Limit: ReadsLight
  - Returns: `200 OK` with `BugReportDto`

- **GET** `/my` - Get user's bug reports
  - Query: `page`, `size`
  - Rate Limit: ReadsLight
  - Returns: `200 OK` with `PagedResult<BugReportDto>`

#### Admin Operations
- **GET** `/` - List all bug reports (Admin only)
  - Query: `page`, `size`
  - Rate Limit: DashboardRead
  - Returns: `200 OK` with `PagedResult<BugReportDto>`

- **GET** `/status/{status}` - Filter by status (Admin only)
  - Query: `page`, `size`
  - Rate Limit: DashboardRead
  - Returns: `200 OK` with `PagedResult<BugReportDto>`

- **PATCH** `/{id}/status` - Update bug report status (Admin only)
  - Body: `BugReportStatusPatchRequest`
  - Rate Limit: ReadsLight
  - Returns: `200 OK` with updated `BugReportDto`

### AbuseController (`/api/abuse`)

- **POST** `/report` - Report abusive chat content
  - Body: `AbuseReportRequest`
  - Rate Limit: BugsWrite
  - Returns: `200 OK` with report ID
  - Creates bug report with abuse metadata

---

## ?? Admin Features

### RolesController (`/api/roles`)

#### Role Management
- **GET** `/` - List roles with pagination and filtering
  - Query: `PageRequest`, `RoleFilter`
  - Returns: `200 OK` with `PagedResult<RoleDto>`

- **GET** `/{id}` - Get role by ID
  - Returns: `200 OK` with `RoleDto`

- **GET** `/exists` - Check if role name exists
  - Query: `name`, `excludeId`
  - Returns: `200 OK` with boolean

- **POST** `/` - Create new role
  - Body: `CreateRoleRequest`
  - Returns: `201 Created` with `RoleDto`

- **PUT** `/{id}` - Update role
  - Body: `UpdateRoleRequest`
  - Returns: `200 OK` with `RoleDto`

- **DELETE** `/{id}` - Hard delete role
  - Returns: `204 No Content`

#### Role Status Management
- **POST** `/{id}/soft-delete` - Soft delete role
  - Returns: `204 No Content`

- **POST** `/{id}/restore` - Restore soft-deleted role
  - Returns: `204 No Content`

#### Batch Operations
- **POST** `/batch-create` - Create multiple roles
  - Body: `IEnumerable<CreateRoleRequest>`
  - Returns: `200 OK` with `BatchResult<Guid, RoleDto>`

- **PUT** `/batch-update` - Update multiple roles
  - Body: `IEnumerable<UpdateItem<Guid, UpdateRoleRequest>>`
  - Returns: `200 OK` with `BatchResult<Guid, RoleDto>`

- **DELETE** `/batch-delete` - Hard delete multiple roles
  - Body: `IEnumerable<Guid>`
  - Returns: `200 OK` with `BatchOutcome<Guid>`

- **POST** `/batch-soft-delete` - Soft delete multiple roles
  - Body: `IEnumerable<Guid>`
  - Returns: `200 OK` with `BatchOutcome<Guid>`

- **POST** `/batch-restore` - Restore multiple roles
  - Body: `IEnumerable<Guid>`
  - Returns: `200 OK` with `BatchOutcome<Guid>`

---

## ?? Rate Limiting

The API implements various rate limiting policies:

- **FriendInvite**: Rate limiting for friend invitations
- **FriendAction**: Rate limiting for friend actions (accept/decline/cancel)
- **GamesRead/GamesWrite**: Rate limiting for game catalog operations
- **EventsWrite**: Rate limiting for event creation/modification
- **ReadsLight/ReadsHeavy**: Different tiers for read operations
- **RegistrationsWrite**: Rate limiting for event registrations
- **RoomsRead/RoomsWrite/RoomsCreate/RoomsArchive**: Various room operation limits
- **ClubsRead/ClubsWrite**: Club operation limits
- **CommunitiesRead/CommunitiesWrite**: Community operation limits
- **PaymentsWrite/PaymentsWebhook**: Payment operation limits
- **BugsWrite**: Bug report creation limits
- **DashboardRead**: Dashboard data access limits
- **TeammatesRead**: Teammate search limits

---

## ?? Common Response Patterns

### Success Responses
- **200 OK**: Successful GET/PUT/PATCH operations
- **201 Created**: Successful POST operations (usually with Location header)
- **204 No Content**: Successful operations with no response body

### Error Responses
- **400 Bad Request**: Validation errors, malformed requests
- **401 Unauthorized**: Missing or invalid authentication
- **403 Forbidden**: Insufficient permissions
- **404 Not Found**: Resource not found
- **409 Conflict**: Resource conflicts (e.g., duplicate names)
- **429 Too Many Requests**: Rate limit exceeded
- **500 Internal Server Error**: Server-side errors

### Pagination
The API supports two pagination patterns:
1. **Cursor-based**: Used for real-time data (friends, games, communities)
2. **Page-based**: Used for administrative operations (users, roles, bug reports)

### Authentication
Most endpoints require JWT Bearer token authentication. Some endpoints are public (marked as `[AllowAnonymous]`).

Admin-only endpoints require the "Admin" role in addition to authentication.
