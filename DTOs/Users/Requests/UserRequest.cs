using System.ComponentModel.DataAnnotations;

namespace DTOs.Users.Requests
{
    // Admin tạo user
    public sealed record CreateUserAdminRequest(
        [param: Required, MaxLength(256)] string UserName,
        [param: Required, EmailAddress, MaxLength(256)] string Email,
        [param: Required, MinLength(6)] string Password,
        [param: MaxLength(256)] string? FullName,
        Gender? Gender,
        [param: MaxLength(256)] string? University,
        int? Level,                           // optional; null => mặc định 1
        [param: Phone] string? PhoneNumber,
        [param: Url] string? AvatarUrl,
        [param: Url] string? CoverUrl,
        bool EmailConfirmed = false,
        string[]? Roles = null
    );

    // Đăng ký tự do (public)
    public sealed record RegisterRequest(
        [param: Required, MaxLength(256)] string FullName,
        Gender Gender,
        [param: MaxLength(256)] string? University,
        [param: EmailAddress, Required, MaxLength(256)] string Email,
        [param: Phone] string? PhoneNumber,
        [param: Required, MinLength(6)] string Password
    );

    // Cập nhật hồ sơ từ Admin (hoặc self với giới hạn)
    public sealed record UpdateUserRequest(
        [param: MaxLength(256)] string? FullName,
        Gender? Gender,
        [param: MaxLength(256)] string? University,
        int? Level,
        [param: Phone] string? PhoneNumber,
        string? AvatarUrl,
        string? CoverUrl,
        bool? EmailConfirmed,
        string[]? ReplaceRoles
    );

    // Cập nhật hồ sơ cá nhân (self-service) - loại bỏ các trường chỉ admin mới được sửa
    public sealed record UpdateUserSelfRequest(
        [param: MaxLength(256)] string? FullName,
        Gender? Gender,
        [param: MaxLength(256)] string? University,
        [param: Phone] string? PhoneNumber,
        string? AvatarUrl,
        string? CoverUrl
    );

    public sealed record ChangePasswordRequest(
        [param: Required] string CurrentPassword,
        [param: Required, MinLength(6)] string NewPassword
    );

    public sealed record ForgotPasswordRequest(
        [param: Required, EmailAddress] string Email
    );

    public sealed record ResetPasswordRequest(
        [param: Required] Guid UserId,
        [param: Required] string Token,
        [param: Required, MinLength(6)] string NewPassword
    );

    public sealed record SetLockoutRequest { public bool Enable { get; init; } public int? Minutes { get; init; } }

    public sealed record ConfirmEmailRequest(
        [param: Required] Guid UserId,
        [param: Required] string Token
    );

    public sealed record ChangeEmailRequest(
        [param: Required] Guid UserId,
        [param: Required, EmailAddress] string NewEmail
    );

    public sealed record ConfirmChangeEmailRequest(
        [param: Required] Guid UserId,
        [param: Required, EmailAddress] string NewEmail,
        [param: Required] string Token
    );

    public sealed record ModifyRolesRequest(string[]? Add, string[]? Remove);

    // Replace all roles
    public sealed class ReplaceRolesRequest
    {
        /// <summary>Danh sách role muốn gán cho user (sẽ thay thế toàn bộ role hiện có).</summary>
        public IReadOnlyCollection<string>? Roles { get; init; }
    }
}
