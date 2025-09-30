using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
namespace Services.Implementations;

public sealed class UsersService : IUserService
{
    private readonly AppDbContext _db;                  
    private readonly IUnitOfWork _uow;                 
    private readonly UserManager<User> _users;
    private readonly RoleManager<Role> _roles;
    private readonly ITimeZoneService _timeZoneService;
    private readonly IEmailQueue _emails;
    private readonly IAuthEmailFactory _authEmails;
    private readonly AuthLinkOptions _authLinks;
    private readonly ITokenService? _tokens;

    // Validators
    private readonly IValidator<UserFilter> _userFilterVal;
    private readonly IValidator<CreateUserAdminRequest> _createVal;
    private readonly IValidator<RegisterRequest> _registerVal;
    private readonly IValidator<UpdateUserRequest> _updateVal;
    private readonly IValidator<SetLockoutRequest> _lockoutVal;
    private readonly IValidator<ReplaceRolesRequest> _replaceRolesVal;
    private readonly IValidator<ModifyRolesRequest> _modifyRolesVal;
    private readonly IValidator<ChangePasswordRequest> _changePwdVal;
    private readonly IValidator<ForgotPasswordRequest> _forgotPwdVal;
    private readonly IValidator<ResetPasswordRequest> _resetPwdVal;
    private readonly IValidator<ConfirmEmailRequest> _confirmEmailVal;
    private readonly IValidator<ChangeEmailRequest> _changeEmailVal;
    private readonly IValidator<ConfirmChangeEmailRequest> _confirmChangeEmailVal;

    public UsersService(
        AppDbContext db,
        IUnitOfWork uow,
        UserManager<User> users,
        RoleManager<Role> roles,
        ITimeZoneService timeZoneService,
        IEmailQueue emails,
        IAuthEmailFactory authEmails,
        AuthLinkOptions authLinks,
        ITokenService? tokens,
        IValidator<UserFilter> userFilterVal,
        IValidator<CreateUserAdminRequest> createVal,
        IValidator<RegisterRequest> registerVal,
        IValidator<UpdateUserRequest> updateVal,
        IValidator<SetLockoutRequest> lockoutVal,
        IValidator<ReplaceRolesRequest> replaceRolesVal,
        IValidator<ModifyRolesRequest> modifyRolesVal,
        IValidator<ChangePasswordRequest> changePwdVal,
        IValidator<ForgotPasswordRequest> forgotPwdVal,
        IValidator<ResetPasswordRequest> resetPwdVal,
        IValidator<ConfirmEmailRequest> confirmEmailVal,
        IValidator<ChangeEmailRequest> changeEmailVal,
        IValidator<ConfirmChangeEmailRequest> confirmChangeEmailVal)
    {
        _db = db;
        _uow = uow;
        _users = users;
        _roles = roles;
        _timeZoneService = timeZoneService;
        _emails = emails;
        _authEmails = authEmails;
        _authLinks = authLinks;
        _tokens = tokens;

        _userFilterVal = userFilterVal;
        _createVal = createVal;
        _registerVal = registerVal;
        _updateVal = updateVal;
        _lockoutVal = lockoutVal;
        _replaceRolesVal = replaceRolesVal;
        _modifyRolesVal = modifyRolesVal;
        _changePwdVal = changePwdVal;
        _forgotPwdVal = forgotPwdVal;
        _resetPwdVal = resetPwdVal;
        _confirmEmailVal = confirmEmailVal;
        _changeEmailVal = changeEmailVal;
        _confirmChangeEmailVal = confirmChangeEmailVal;
    }

    // ---------- READ (DB giữ UTC, chỉ convert khi map DTO) ----------
    public async Task<Result<PagedResult<UserListItemDto>>> SearchAsync(
        UserFilter filter, PageRequest page, CancellationToken ct = default)
    {
        // NOTE: filter.CreatedFromUtc / CreatedToUtc là UTC thật
        return await _userFilterVal.ValidateToResultAsync(filter, ct)
            .BindAsync(async _ =>
            {
                IQueryable<User> q = _db.Users.AsNoTracking()
                    .ApplyKeyword(filter.Keyword)
                    .ApplyFlags(filter.EmailConfirmed, filter.LockedOnly)
                    .ApplyCreatedUtcRange(filter.CreatedFromUtc, filter.CreatedToUtc)
                    .ApplySort(filter.SortBy, filter.Desc);

                var usersPage = await q.ToPagedResultAsync(page, ct);
                var userIds = usersPage.Items.Select(u => u.Id).ToArray();

                // Nạp roles map một lượt (tránh N+1)
                var rolesMap = await (
                    from ur in _db.UserRoles.AsNoTracking()
                    join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                    where userIds.Contains(ur.UserId)
                    select new { ur.UserId, r.Name }
                )
                .GroupBy(x => x.UserId)
                .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.Name!).ToArray(), ct);

                var nowUtc = DateTimeOffset.UtcNow;

                var items = usersPage.Items.Select(u =>
                    u.ToListItemDto(
                        _timeZoneService,
                        rolesMap.TryGetValue(u.Id, out var names) ? names : Array.Empty<string>(),
                        nowUtc)
                ).ToList();

                var result = new PagedResult<UserListItemDto>(
                    items,
                    usersPage.Page,
                    usersPage.Size,
                    usersPage.TotalCount,
                    usersPage.TotalPages,
                    usersPage.HasPrevious,
                    usersPage.HasNext,
                    usersPage.Sort,
                    usersPage.Desc);

                return Result<PagedResult<UserListItemDto>>.Success(result);
            });
    }

    public async Task<Result<UserDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _users.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return Result<UserDetailDto>.Failure(IdentityResultExtensions.NotFound("User"));

        var dto = await user.ToDetailDtoAsync(_users, _timeZoneService, ct); // convert UTC -> VN ở mapper
        return Result<UserDetailDto>.Success(dto);
    }

    // ---------- CREATE / UPDATE ----------
    public async Task<Result<UserDetailDto>> CreateAsync(CreateUserAdminRequest req, CancellationToken ct = default)
    {
        return await _createVal.ValidateToResultAsync(req, ct)
     .BindAsync(_ => _uow.ExecuteTransactionAsync(async ctk =>
     {
         var user = new User
         {
             UserName = req.UserName?.Trim(),
             Email = req.Email?.Trim(),
             FullName = req.FullName?.Trim(),
             PhoneNumber = req.PhoneNumber?.Trim(),
             EmailConfirmed = req.EmailConfirmed
         };

         var create = await _users.CreateAsync(user, req.Password);
         if (!create.Succeeded)
             return create.ToResult<UserDetailDto>(null!, "Create user failed");

         var rolesToAssign = await ResolveRolesToAssignAsync(req.Roles, ctk);
         var addRoles = await _users.AddToRolesAsync(user, rolesToAssign);
         if (!addRoles.Succeeded)
             return addRoles.ToResult<UserDetailDto>(null!, "Assign roles failed");

         await _uow.SaveChangesAsync(ctk);

         var dto = await user.ToDetailDtoAsync(_users, _timeZoneService, ctk);
         return Result<UserDetailDto>.Success(dto);
     }, ct: ct))
     .TapAsync(async dto =>
     {
         if (!dto.EmailConfirmed &&
             !string.IsNullOrWhiteSpace(dto.Email) &&
             !string.IsNullOrWhiteSpace(_authLinks.PublicBaseUrl))
         {
             var user = await _users.FindByIdAsync(dto.Id.ToString());
             if (user is not null)
             {
                 var token = await _users.GenerateEmailConfirmationTokenAsync(user);
                 var url = BuildPublicLink(
                     _authLinks.PublicBaseUrl,
                     _authLinks.ConfirmEmailPath,
                     new() { ["uid"] = user.Id.ToString(), ["token"] = token.Base64UrlEncodeUtf8() });

                 await _emails.EnqueueAsync(_authEmails.BuildConfirmEmail(user, url), ct);
             }
         }
     });
    }

    public async Task<Result<UserDetailDto>> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        return await _registerVal.ValidateToResultAsync(req, ct)
            .BindAsync(_ => _uow.ExecuteTransactionAsync<UserDetailDto>(async ctk =>
            {
                var userName = await GenerateUniqueUserNameAsync(req.Email, ctk);

                var user = new User
                {
                    UserName = userName,
                    Email = req.Email.Trim(),
                    FullName = req.FullName?.Trim(),
                    Gender = req.Gender,
                    University = req.University?.Trim(),
                    PhoneNumber = req.PhoneNumber?.Trim(),
                    EmailConfirmed = false
                };

                var create = await _users.CreateAsync(user, req.Password);
                if (!create.Succeeded)
                    return create.ToResult<UserDetailDto>(null!, "Register failed");

                var rolesToAssign = await EnsureDefaultRoleAsync(ctk);
                var addRoles = await _users.AddToRolesAsync(user, rolesToAssign);
                if (!addRoles.Succeeded)
                    return addRoles.ToResult<UserDetailDto>(null!, "Assign roles failed");

                await _uow.SaveChangesAsync(ctk);

                var dto = await user.ToDetailDtoAsync(_users, _timeZoneService, ctk);
                return Result<UserDetailDto>.Success(dto);
            }, ct: ct)) // <-- dùng named argument 'ct'
            .TapAsync(async dto =>
            {
                if (!dto.EmailConfirmed &&
                    !string.IsNullOrWhiteSpace(dto.Email) &&
                    !string.IsNullOrWhiteSpace(_authLinks.PublicBaseUrl))
                {
                    var user = await _users.FindByIdAsync(dto.Id.ToString());
                    if (user is not null)
                    {
                        var token = await _users.GenerateEmailConfirmationTokenAsync(user);
                        var url = BuildPublicLink(
                            _authLinks.PublicBaseUrl,
                            string.IsNullOrWhiteSpace(_authLinks.ConfirmEmailPath) ? "auth/confirm-email" : _authLinks.ConfirmEmailPath,
                            new() { ["uid"] = user.Id.ToString(), ["token"] = token.Base64UrlEncodeUtf8() });

                        await _emails.EnqueueAsync(_authEmails.BuildConfirmEmail(user, url), ct);
                    }
                }
            });
    }

    public async Task<Result> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct = default)
    {
        return await _updateVal.ValidateToResultAsync(req, ct)
            .BindAsync(async _ =>
            {
                var user = await _users.FindByIdAsync(id.ToString());
                if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));

                user.FullName = req.FullName?.Trim();
                user.PhoneNumber = req.PhoneNumber?.Trim();

                var update = await _users.UpdateAsync(user);
                return update.ToResult("Update user failed");
            });
    }

    // ---------- LOCKOUT ----------
    public async Task<Result> SetLockoutAsync(Guid id, SetLockoutRequest req, CancellationToken ct = default)
    {
        return await _lockoutVal.ValidateToResultAsync(req, ct)
            .BindAsync(_ => _uow.ExecuteTransactionAsync<Result>(async ctk =>
            {
                var user = await _users.FindByIdAsync(id.ToString());
                if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));

                await _users.SetLockoutEnabledAsync(user, true);

                DateTimeOffset? until = null;
                if (req.Enable)
                    until = req.Minutes.HasValue ? DateTimeOffset.UtcNow.AddMinutes(req.Minutes.Value) : DateTimeOffset.MaxValue;

                var r = await _users.SetLockoutEndDateAsync(user, until);
                if (!r.Succeeded) return r.ToResult(req.Enable ? "Lockout failed" : "Unlock failed");

                await _uow.SaveChangesAsync(ctk);
                return Result.Success();
            },ct: ct));
    }

    // ---------- ROLES ----------
    public async Task<Result> ReplaceRolesAsync(Guid id, ReplaceRolesRequest req, CancellationToken ct = default)
    {
        return await _replaceRolesVal.ValidateToResultAsync(req, ct)
            .BindAsync(_ => _uow.ExecuteTransactionAsync<Result>(async ctk =>
            {
                var user = await _users.FindByIdAsync(id.ToString());
                if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));

                var current = await _users.GetRolesAsync(user);

                var desired = (req.Roles ?? Array.Empty<string>())
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var desiredNorm = new HashSet<string>(desired.Select(x => x.ToUpperInvariant()));
                var validDesired = await _roles.Roles
                    .Where(r => desiredNorm.Contains(r.NormalizedName!))
                    .Select(r => r.Name!)
                    .ToListAsync(ctk);

                if (validDesired.Count == 0)
                    validDesired = (await EnsureDefaultRoleAsync(ctk)).ToList();

                var toRemove = current.Except(validDesired, StringComparer.OrdinalIgnoreCase).ToArray();
                var toAdd = validDesired.Except(current, StringComparer.OrdinalIgnoreCase).ToArray();

                if (toRemove.Length > 0)
                {
                    var r1 = await _users.RemoveFromRolesAsync(user, toRemove);
                    if (!r1.Succeeded) return r1.ToResult("Remove roles failed");
                }

                if (toAdd.Length > 0)
                {
                    var r2 = await _users.AddToRolesAsync(user, toAdd);
                    if (!r2.Succeeded) return r2.ToResult("Add roles failed");
                }

                await _uow.SaveChangesAsync(ctk);
                return Result.Success();
            },ct: ct));
    }

    public async Task<Result> ModifyRolesAsync(Guid id, ModifyRolesRequest req, CancellationToken ct = default)
    {
        return await _modifyRolesVal.ValidateToResultAsync(req, ct)
            .BindAsync(_ => _uow.ExecuteTransactionAsync<Result>(async ctk =>
            {
                var user = await _users.FindByIdAsync(id.ToString());
                if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));

                if (req.Add is { Length: > 0 })
                {
                    var desiredNorm = new HashSet<string>(
                        req.Add.Where(s => !string.IsNullOrWhiteSpace(s))
                               .Select(s => s.Trim().ToUpperInvariant()));

                    var validAdd = await _roles.Roles
                        .Where(r => desiredNorm.Contains(r.NormalizedName!))
                        .Select(r => r.Name!)
                        .ToArrayAsync(ctk);

                    if (validAdd.Length > 0)
                    {
                        var r1 = await _users.AddToRolesAsync(user, validAdd);
                        if (!r1.Succeeded) return r1.ToResult("Add roles failed");
                    }
                }

                if (req.Remove is { Length: > 0 })
                {
                    var r2 = await _users.RemoveFromRolesAsync(user, req.Remove);
                    if (!r2.Succeeded) return r2.ToResult("Remove roles failed");
                }

                await _uow.SaveChangesAsync(ctk);
                return Result.Success();
            },ct: ct));
    }

    // ---------- PASSWORD ----------
    public async Task<Result> ChangePasswordAsync(Guid id, ChangePasswordRequest req, CancellationToken ct = default)
    {
        return await _changePwdVal.ValidateToResultAsync(req, ct)
            .BindAsync(_ => _uow.ExecuteTransactionAsync(async ctk =>   // <-- KHÔNG dùng <Result>
            {
                var user = await _users.FindByIdAsync(id.ToString());
                if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));

                var r = await _users.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
                if (!r.Succeeded) return r.ToResult("Change password failed");

                if (_tokens is not null)
                    await _tokens.RevokeAllForUserAsync(user.Id, reason: "password changed", ct: ctk);

                await _uow.SaveChangesAsync(ctk);
                return Result.Success();
            }, ct: ct)) // <-- named argument
            .TapAsync(async () =>
            {
                var user = await _users.FindByIdAsync(id.ToString());
                if (user is not null && !string.IsNullOrWhiteSpace(user.Email))
                    await _emails.EnqueueAsync(_authEmails.BuildPasswordChanged(user), ct);
            });
    }

    public async Task<Result<string>> GenerateForgotPasswordTokenAsync(ForgotPasswordRequest req, CancellationToken ct = default)
    {
        return await _forgotPwdVal.ValidateToResultAsync(req, ct)
            .BindAsync(async _ =>
            {
                var user = await FindByUserNameOrEmailAsync(req.Email, ct);
                if (user is null) return Result<string>.Failure(IdentityResultExtensions.NotFound("User"));

                var token = await _users.GeneratePasswordResetTokenAsync(user);
                return Result<string>.Success(token);
            });
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest req, CancellationToken ct = default)
    {
        return await _resetPwdVal.ValidateToResultAsync(req, ct)
            .BindAsync(_ => _uow.ExecuteTransactionAsync(async ctk =>   // <-- KHÔNG dùng <Result>
            {
                var user = await _users.FindByIdAsync(req.UserId.ToString());
                if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));

                var token = req.Token.DecodeTokenIfNeeded();
                var r = await _users.ResetPasswordAsync(user, token, req.NewPassword);
                if (!r.Succeeded) return r.ToResult("Reset password failed");

                if (_tokens is not null)
                    await _tokens.RevokeAllForUserAsync(user.Id, reason: "password reset", ct: ctk);

                await _uow.SaveChangesAsync(ctk);
                return Result.Success();
            }, ct: ct)) // <-- named argument
            .TapAsync(async () =>
            {
                var user = await _users.FindByIdAsync(req.UserId.ToString());
                if (user is not null && !string.IsNullOrWhiteSpace(user.Email))
                    await _emails.EnqueueAsync(_authEmails.BuildPasswordResetSucceeded(user), ct);
            });
    }

    // ---------- EMAIL CONFIRM / CHANGE EMAIL ----------
    public async Task<Result> ConfirmEmailAsync(ConfirmEmailRequest req, CancellationToken ct = default)
    {
        return await _confirmEmailVal.ValidateToResultAsync(req, ct)
            .BindAsync(async _ =>
            {
                var user = await _users.FindByIdAsync(req.UserId.ToString());
                if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));

                var token = req.Token.DecodeTokenIfNeeded();
                var r = await _users.ConfirmEmailAsync(user, token);
                return r.ToResult("Confirm email failed");
            });
    }

    public async Task<Result<string>> GenerateEmailConfirmTokenAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return Result<string>.Failure(IdentityResultExtensions.NotFound("User"));

        var token = await _users.GenerateEmailConfirmationTokenAsync(user);
        return Result<string>.Success(token);
    }

    public async Task<Result> ConfirmChangeEmailAsync(ConfirmChangeEmailRequest req, CancellationToken ct = default)
    {
        return await _confirmChangeEmailVal.ValidateToResultAsync(req, ct)
            .BindAsync(async _ =>
            {
                var user = await _users.FindByIdAsync(req.UserId.ToString());
                if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));

                var token = req.Token.DecodeTokenIfNeeded();
                var r = await _users.ChangeEmailAsync(user, req.NewEmail, token);
                return r.ToResult("Change email failed");
            });
    }

    public async Task<Result<string>> GenerateChangeEmailTokenAsync(Guid id, ChangeEmailRequest req, CancellationToken ct = default)
    {
        return await _changeEmailVal.ValidateToResultAsync(req, ct)
            .BindAsync(async _ =>
            {
                var user = await _users.FindByIdAsync(id.ToString());
                if (user is null) return Result<string>.Failure(IdentityResultExtensions.NotFound("User"));

                var token = await _users.GenerateChangeEmailTokenAsync(user, req.NewEmail);
                return Result<string>.Success(token);
            });
    }

    public async Task<Result> SendEmailConfirmAsync(Guid id, string callbackBaseUrl, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));
        if (string.IsNullOrWhiteSpace(user.Email))
            return Result.Failure(new Error(Error.Codes.Validation, "User has no email."));
        if (user.EmailConfirmed) return Result.Success();

        var token = await _users.GenerateEmailConfirmationTokenAsync(user);

        var baseUrl = !string.IsNullOrWhiteSpace(_authLinks.PublicBaseUrl) ? _authLinks.PublicBaseUrl : callbackBaseUrl;
        var path = string.IsNullOrWhiteSpace(_authLinks.ConfirmEmailPath) ? "auth/confirm-email" : _authLinks.ConfirmEmailPath;

        var url = BuildPublicLink(baseUrl, path, new()
        {
            ["uid"] = user.Id.ToString(),
            ["token"] = token.Base64UrlEncodeUtf8()
        });

        await _emails.EnqueueAsync(_authEmails.BuildConfirmEmail(user, url), ct);
        return Result.Success();
    }

    public async Task<Result> SendPasswordResetEmailAsync(ForgotPasswordRequest req, string? callbackBaseUrl, CancellationToken ct = default)
    {
        return await _forgotPwdVal.ValidateToResultAsync(req, ct)
            .BindAsync(async _ =>
            {
                // privacy-preserving: không lộ user
                var user = await FindByUserNameOrEmailAsync(req.Email, ct);

                var baseUrl = !string.IsNullOrWhiteSpace(_authLinks.PublicBaseUrl)
                    ? _authLinks.PublicBaseUrl
                    : callbackBaseUrl;

                if (string.IsNullOrWhiteSpace(baseUrl))
                    return Result.Failure(new Error(Error.Codes.Validation, "callbackBaseUrl is required (or configure AuthLinks:PublicBaseUrl)."));

                if (user is not null && await _users.IsEmailConfirmedAsync(user))
                {
                    var token = await _users.GeneratePasswordResetTokenAsync(user);
                    var path = string.IsNullOrWhiteSpace(_authLinks.ResetPasswordPath) ? "auth/reset-password" : _authLinks.ResetPasswordPath;

                    var url = BuildPublicLink(baseUrl, path, new()
                    {
                        ["uid"] = user.Id.ToString(),
                        ["token"] = token.Base64UrlEncodeUtf8()
                    });

                    await _emails.EnqueueAsync(_authEmails.BuildResetPassword(user, url), ct);
                }

                return Result.Success();
            });
    }

    public async Task<Result> SendChangeEmailConfirmAsync(Guid id, ChangeEmailRequest req, string callbackBaseUrl, CancellationToken ct = default)
    {
        return await _changeEmailVal.ValidateToResultAsync(req, ct)
            .BindAsync(async _ =>
            {
                var user = await _users.FindByIdAsync(id.ToString());
                if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));
                if (string.Equals(user.Email, req.NewEmail, StringComparison.OrdinalIgnoreCase))
                    return Result.Success();

                var token = await _users.GenerateChangeEmailTokenAsync(user, req.NewEmail);
                var baseUrl = !string.IsNullOrWhiteSpace(_authLinks.PublicBaseUrl) ? _authLinks.PublicBaseUrl : callbackBaseUrl;
                var path = string.IsNullOrWhiteSpace(_authLinks.ChangeEmailPath) ? "auth/confirm-change-email" : _authLinks.ChangeEmailPath;

                var url = BuildPublicLink(baseUrl, path, new()
                {
                    ["uid"] = user.Id.ToString(),
                    ["newEmail"] = req.NewEmail,
                    ["token"] = token.Base64UrlEncodeUtf8()
                });

                await _emails.EnqueueAsync(_authEmails.BuildChangeEmail(req.NewEmail, url, user.FullName ?? user.UserName), ct);
                return Result.Success();
            });
    }

    // ---------- HELPERS ----------
    private async Task<User?> FindByUserNameOrEmailAsync(string userNameOrEmail, CancellationToken ct)
    {
        var normalized = (userNameOrEmail ?? string.Empty).Trim();
        var byEmail = await _users.FindByEmailAsync(normalized);
        if (byEmail is not null) return byEmail;
        return await _users.FindByNameAsync(normalized);
    }

    private async Task<string[]> EnsureDefaultRoleAsync(CancellationToken ct)
    {
        const string fallback = "User";
        if (!await _roles.RoleExistsAsync(fallback))
        {
            var createRole = await _roles.CreateAsync(new Role
            {
                Name = fallback,
                NormalizedName = fallback.ToUpperInvariant()
            });
            if (!createRole.Succeeded && !await _roles.RoleExistsAsync(fallback))
                throw new InvalidOperationException("Create fallback role 'User' failed.");
        }
        return new[] { fallback };
    }

    private async Task<string[]> ResolveRolesToAssignAsync(string[]? roles, CancellationToken ct)
    {
        if (roles is not { Length: > 0 }) return await EnsureDefaultRoleAsync(ct);

        var desiredNorm = new HashSet<string>(
            roles.Where(r => !string.IsNullOrWhiteSpace(r))
                 .Select(r => r.Trim().ToUpperInvariant()));

        var valid = await _roles.Roles
            .Where(r => desiredNorm.Contains(r.NormalizedName!))
            .Select(r => r.Name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArrayAsync(ct);

        return valid.Length == 0 ? await EnsureDefaultRoleAsync(ct) : valid;
    }

    private async Task<string> GenerateUniqueUserNameAsync(string email, CancellationToken ct)
    {
        var at = email.IndexOf('@');
        var local = (at > 0 ? email[..at] : email).Trim();

        var baseName = Regex.Replace(local.ToLowerInvariant(), @"[^a-z0-9._-]", string.Empty);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "user";
        if (baseName.Length < 3)
            baseName = (baseName + "user").Substring(0, Math.Min(16, (baseName + "user").Length));

        var candidate = baseName;
        var i = 0;
        while (await _users.FindByNameAsync(candidate) is not null)
        {
            i++;
            candidate = $"{baseName}{i}";
            if (i > 9999)
            {
                candidate = $"{baseName}{Guid.NewGuid():N}".Substring(0, Math.Min(24, baseName.Length + 8));
                break;
            }
        }
        return candidate;
    }
}
