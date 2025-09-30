using System.ComponentModel.DataAnnotations;

namespace DTOs.Users.Requests
{
    // Admin tạo user
    public sealed record CreateUserAdminRequest(
        [property: Required, MaxLength(256)] string UserName,
        [property: Required, EmailAddress, MaxLength(256)] string Email,
        [property: Required, MinLength(6)] string Password,
        [property: MaxLength(256)] string? FullName,
        Gender? Gender,
        [property: MaxLength(256)] string? University,
        int? Level,                           // optional; null => mặc định 1
        [property: Phone] string? PhoneNumber,
        [property: Url] string? AvatarUrl,
        [property: Url] string? CoverUrl,
        bool EmailConfirmed = false,
        string[]? Roles = null
    );

    // Đăng ký tự do (public)
    public sealed record RegisterRequest(
        [property: Required, MaxLength(256)] string FullName,
        Gender Gender,
        [property: MaxLength(256)] string? University,
        [property: EmailAddress, Required, MaxLength(256)] string Email,
        [property: Phone] string? PhoneNumber,
        [property: Required, MinLength(6)] string Password
    );

    // Cập nhật hồ sơ từ Admin (hoặc self với giới hạn)
    public sealed record UpdateUserRequest(
        [MaxLength(256)] string? FullName,
        Gender? Gender,
        [MaxLength(256)] string? University,
        int? Level,
        [Phone] string? PhoneNumber,
        string? AvatarUrl,
        string? CoverUrl,
        bool? EmailConfirmed,
        string[]? ReplaceRoles
    );

    // Cập nhật hồ sơ cá nhân (self-service) - loại bỏ các trường chỉ admin mới được sửa
    public sealed record UpdateUserSelfRequest(
        [MaxLength(256)] string? FullName,
        Gender? Gender,
        [MaxLength(256)] string? University,
        [Phone] string? PhoneNumber,
        string? AvatarUrl,
        string? CoverUrl
    );

    public sealed record ChangePasswordRequest([Required] string CurrentPassword,
                                               [Required, MinLength(6)] string NewPassword);

    public sealed record ForgotPasswordRequest([Required, EmailAddress] string Email);

    public sealed record ResetPasswordRequest([Required] Guid UserId,
                                              [Required] string Token,
                                              [Required, MinLength(6)] string NewPassword);

    public sealed record SetLockoutRequest { public bool Enable { get; init; } public int? Minutes { get; init; } }

    public sealed record ConfirmEmailRequest([Required] Guid UserId, [Required] string Token);

    public sealed record ChangeEmailRequest([Required] Guid UserId, [Required, EmailAddress] string NewEmail);

    public sealed record ConfirmChangeEmailRequest([Required] Guid UserId,
                                                   [Required, EmailAddress] string NewEmail,
                                                   [Required] string Token);

    public sealed record ModifyRolesRequest(string[]? Add, string[]? Remove);

    // Replace all roles
    public sealed class ReplaceRolesRequest
    {
        /// <summary>Danh sách role muốn gán cho user (sẽ thay thế toàn bộ role hiện có).</summary>
        public IReadOnlyCollection<string>? Roles { get; init; }
    }
}
