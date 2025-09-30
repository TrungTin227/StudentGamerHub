namespace Services.Interfaces;

public interface IUserService
{
    // ------------ READ ------------
    Task<Result<PagedResult<UserListItemDto>>> SearchAsync(UserFilter filter, PageRequest page, CancellationToken ct = default);
    Task<Result<UserDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    // ------------ WRITE ------------
    Task<Result<UserDetailDto>> CreateAsync(CreateUserAdminRequest req, CancellationToken ct = default);
    Task<Result> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct = default);
    Task<Result> UpdateSelfAsync(Guid id, UpdateUserSelfRequest req, CancellationToken ct = default);
    Task<Result<UserDetailDto>> RegisterAsync(RegisterRequest req, CancellationToken ct = default);


    // ------------ LOCKOUT ------------
    Task<Result> SetLockoutAsync(Guid id, SetLockoutRequest req, CancellationToken ct = default);

    // ------------ ROLES ------------
    Task<Result> ReplaceRolesAsync(Guid id, ReplaceRolesRequest req, CancellationToken ct = default);
    Task<Result> ModifyRolesAsync(Guid id, ModifyRolesRequest req, CancellationToken ct = default);

    // ------------ PASSWORD FLOW ------------
    Task<Result> ChangePasswordAsync(Guid id, ChangePasswordRequest req, CancellationToken ct = default);
    Task<Result> SendPasswordResetEmailAsync(ForgotPasswordRequest req, string? callbackBaseUrl, CancellationToken ct = default);
    Task<Result> ResetPasswordAsync(ResetPasswordRequest req, CancellationToken ct = default);

    // ------------ EMAIL CONFIRM FLOW ------------
    Task<Result> SendEmailConfirmAsync(Guid id, string callbackBaseUrl, CancellationToken ct = default);
    Task<Result> ConfirmEmailAsync(ConfirmEmailRequest req, CancellationToken ct = default);

    // ------------ CHANGE EMAIL FLOW ------------
    Task<Result> SendChangeEmailConfirmAsync(Guid id, ChangeEmailRequest req, string callbackBaseUrl, CancellationToken ct = default);
    Task<Result> ConfirmChangeEmailAsync(ConfirmChangeEmailRequest req, CancellationToken ct = default);

#if DEBUG
    // DEV-ONLY: tiện test thủ công, KHÔNG dùng production
    Task<Result<string>> GenerateForgotPasswordTokenAsync(ForgotPasswordRequest req, CancellationToken ct = default);
    Task<Result<string>> GenerateEmailConfirmTokenAsync(Guid id, CancellationToken ct = default);
    Task<Result<string>> GenerateChangeEmailTokenAsync(Guid id, ChangeEmailRequest req, CancellationToken ct = default);
#endif
}
