using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Services.Common.Results;
using System.Text.RegularExpressions;
using static Services.Common.Extensions.StringAndUrlExtensions;

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
    private readonly IValidator<UpdateUserSelfRequest> _updateSelfVal;
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
        IValidator<UpdateUserSelfRequest> updateSelfVal,
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
        _updateSelfVal = updateSelfVal;
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

                var nowUtc = DateTime.UtcNow;

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

        var dto = await user.ToDetailDtoAsync(_users, _timeZoneService, ct);
        return Result<UserDetailDto>.Success(dto);
    }

    // ---------- CREATE / UPDATE ----------
    public async Task<Result<UserDetailDto>> CreateAsync(CreateUserAdminRequest req, CancellationToken ct = default)
    {
        return await _createVal.ValidateToResultAsync(req, ct)
            .BindAsync(_ => _uow.ExecuteTransactionAsync<UserDetailDto>(async ctk =>
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
                
                // ✅ FIX: Add roles one by one instead of batch
                var addRolesResult = await AddRolesToUserAsync(user, rolesToAssign);
                if (!addRolesResult.IsSuccess)
                    return Result<UserDetailDto>.Failure(addRolesResult.Error);

                await _uow.SaveChangesAsync(ctk);

                var dto = await user.ToDetailDtoAsync(_users, _timeZoneService, ctk);
                return Result<UserDetailDto>.Success(dto);
            }, ct: ct))
            .TapAsync(dto => SendConfirmEmailIfNeededAsync(dto, ct));
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
                
                // ✅ FIX: Add roles one by one instead of batch
                var addRolesResult = await AddRolesToUserAsync(user, rolesToAssign);
                if (!addRolesResult.IsSuccess)
                    return Result<UserDetailDto>.Failure(addRolesResult.Error);

                await _uow.SaveChangesAsync(ctk);

                var dto = await user.ToDetailDtoAsync(_users, _timeZoneService, ct);
                return Result<UserDetailDto>.Success(dto);
            }, ct: ct))
            .TapAsync(dto => SendConfirmEmailIfNeededAsync(dto, ct));
    }

    public async Task<Result> UpdateAsync(Guid id, UpdateUserRequest req, CancellationToken ct = default)
    {
        return await _updateVal.ValidateToResultAsync(req, ct)
            .BindAsync(_ => _uow.ExecuteTransactionAsync<Result>(async ctk =>
            {
                var user = await _users.FindByIdAsync(id.ToString());
                if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));

                user.FullName = req.FullName?.Trim();
                user.Gender = req.Gender ?? user.Gender;
                user.University = req.University?.Trim();
                user.Level = req.Level ?? user.Level;
                user.PhoneNumber = req.PhoneNumber?.Trim();
                user.AvatarUrl = req.AvatarUrl?.Trim();
                user.CoverUrl = req.CoverUrl?.Trim();
                user.EmailConfirmed = req.EmailConfirmed ?? user.EmailConfirmed;

                var update = await _users.UpdateAsync(user);
                if (!update.Succeeded) return update.ToResult("Update user failed");

                if (req.ReplaceRoles is not null)
                {
                    var rr = await ReplaceRolesCoreAsync(user, req.ReplaceRoles, ctk);
                    if (!rr.IsSuccess) return rr;
                }

                await _uow.SaveChangesAsync(ctk);
                return Result.Success();
            }, ct: ct));
    }

    public async Task<Result> UpdateSelfAsync(Guid id, UpdateUserSelfRequest req, CancellationToken ct = default)
    {
        return await _updateSelfVal.ValidateToResultAsync(req, ct)
            .BindAsync(async _ =>
            {
                var user = await _users.FindByIdAsync(id.ToString());
                if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));

                // Only update allowed fields for self-service
                user.FullName = req.FullName?.Trim();
                user.Gender = req.Gender ?? user.Gender;
                user.University = req.University?.Trim();
                user.PhoneNumber = req.PhoneNumber?.Trim();
                user.AvatarUrl = req.AvatarUrl?.Trim();
                user.CoverUrl = req.CoverUrl?.Trim();

                var update = await _users.UpdateAsync(user);
                return update.ToResult("Update user profile failed");
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

                DateTime? until = null;
                if (req.Enable)
                    until = req.Minutes.HasValue ? DateTime.UtcNow.AddMinutes(req.Minutes.Value) : DateTime.MaxValue;

                var lockoutEnd = until.HasValue
                    ? new DateTimeOffset(DateTime.SpecifyKind(until.Value, DateTimeKind.Utc), TimeSpan.Zero)
                    : (DateTimeOffset?)null;

                var r = await _users.SetLockoutEndDateAsync(user, lockoutEnd);
                if (!r.Succeeded) return r.ToResult(req.Enable ? "Lockout failed" : "Unlock failed");

                await _uow.SaveChangesAsync(ctk);
                return Result.Success();
            }, ct: ct));
    }

    // ---------- ROLES ----------
    public async Task<Result> ReplaceRolesAsync(Guid id, ReplaceRolesRequest req, CancellationToken ct = default)
    {
        return await _replaceRolesVal.ValidateToResultAsync(req, ct)
            .BindAsync(_ => _uow.ExecuteTransactionAsync<Result>(async ctk =>
            {
                var user = await _users.FindByIdAsync(id.ToString());
                if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));

                var rr = await ReplaceRolesCoreAsync(user, req.Roles ?? Array.Empty<string>(), ctk);
                if (!rr.IsSuccess) return rr;

                await _uow.SaveChangesAsync(ctk);
                return Result.Success();
            }, ct: ct));
    }

    public async Task<Result> ModifyRolesAsync(Guid id, ModifyRolesRequest req, CancellationToken ct = default)
    {
        return await _modifyRolesVal.ValidateToResultAsync(req, ct)
            .BindAsync(_ => _uow.ExecuteTransactionAsync<Result>(async ctk =>
            {
                var user = await _users.FindByIdAsync(id.ToString());
                if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));

                var current = await _users.GetRolesAsync(user);

                // ---- ADD ----
                if (req.Add is { Length: > 0 })
                {
                    var addSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var s in req.Add.Where(s => !string.IsNullOrWhiteSpace(s)))
                        addSet.Add(s.Trim());

                    var addNorm = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var s in addSet)
                        addNorm.Add(s.ToUpperInvariant());

                    // ✅ Convert to List for EF Core compatibility
                    var addNormList = addNorm.ToList();

                    // ⚡ LOAD ALL ROLES
                    var allRoles = await _db.Roles
                        .AsNoTracking()
                        .Select(r => new { r.Name, r.NormalizedName })
                        .ToListAsync(ctk);

                    // ✅ Filter in-memory
                    var addNames = allRoles
                        .Where(r => addNormList.Contains(r.NormalizedName!))
                        .Select(r => r.Name!)
                        .ToArray();

                    var toAddSet = new HashSet<string>(addNames, StringComparer.OrdinalIgnoreCase);
                    foreach (var c in current) toAddSet.Remove(c);
                    var toAdd = toAddSet.ToArray();

                    if (toAdd.Length > 0)
                    {
                        var r1 = await AddRolesToUserAsync(user, toAdd);
                        if (!r1.IsSuccess) return r1;
                    }
                }
                // ---- REMOVE ----
                if (req.Remove is { Length: > 0 })
                {
                    var removeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var s in req.Remove.Where(s => !string.IsNullOrWhiteSpace(s)))
                        removeSet.Add(s.Trim());

                    var toRemove = current.Where(r => removeSet.Contains(r)).ToArray();

                    if (toRemove.Length > 0)
                    {
                        foreach (var roleName in toRemove)
                        {
                            var r2 = await _users.RemoveFromRoleAsync(user, roleName);
                            if (!r2.Succeeded) return r2.ToResult($"Remove role '{roleName}' failed");
                        }
                    }
                }

                await _uow.SaveChangesAsync(ctk);
                return Result.Success();
            }, ct: ct));
    }


    // ---------- PASSWORD ----------
    public async Task<Result> ChangePasswordAsync(Guid id, ChangePasswordRequest req, CancellationToken ct = default)
    {
        var validated = await _changePwdVal.ValidateToResultAsync(req, ct);
        if (!validated.IsSuccess) return Result.Failure(validated.Error);

        User? userForEmail = null;

        var result = await _uow.ExecuteTransactionAsync(async ctk =>
        {
            var user = await _users.FindByIdAsync(id.ToString());
            if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));

            var r = await _users.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
            if (!r.Succeeded) return r.ToResult("Change password failed");

            if (_tokens is not null)
                await _tokens.RevokeAllForUserAsync(user.Id, reason: "password changed", ct: ctk);

            await _uow.SaveChangesAsync(ctk);

            userForEmail = user;
            return Result.Success();
        }, ct: ct);

        if (result.IsSuccess && userForEmail is not null && !string.IsNullOrWhiteSpace(userForEmail.Email))
            await _emails.EnqueueAsync(_authEmails.BuildPasswordChanged(userForEmail), ct);

        return result;
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
        var validated = await _resetPwdVal.ValidateToResultAsync(req, ct);
        if (!validated.IsSuccess) return Result.Failure(validated.Error);

        User? userForEmail = null;

        var result = await _uow.ExecuteTransactionAsync(async ctk =>
        {
            var user = await _users.FindByIdAsync(req.UserId.ToString());
            if (user is null) return Result.Failure(IdentityResultExtensions.NotFound("User"));

            var token = req.Token.DecodeTokenIfNeeded();
            var r = await _users.ResetPasswordAsync(user, token, req.NewPassword);
            if (!r.Succeeded) return r.ToResult("Reset password failed");

            if (_tokens is not null)
                await _tokens.RevokeAllForUserAsync(user.Id, reason: "password reset", ct: ctk);

            await _uow.SaveChangesAsync(ctk);

            userForEmail = user;
            return Result.Success();
        }, ct: ct);

        if (result.IsSuccess && userForEmail is not null && !string.IsNullOrWhiteSpace(userForEmail.Email))
            await _emails.EnqueueAsync(_authEmails.BuildPasswordResetSucceeded(userForEmail), ct);

        return result;
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
        const string normalized = "USER";

        // ✅ Query trực tiếp từ DbContext, KHÔNG dùng RoleManager
        var exists = await _db.Roles
            .AsNoTracking()
            .AnyAsync(r => r.NormalizedName == normalized, ct);

        if (!exists)
        {
            var role = new Role
            {
                Id = Guid.NewGuid(),
                Name = fallback,
                NormalizedName = normalized,
                ConcurrencyStamp = Guid.NewGuid().ToString()
            };

            _db.Roles.Add(role);
            await _db.SaveChangesAsync(ct);
        }

        return new[] { fallback };
    }
    private async Task<string[]> ResolveRolesToAssignAsync(string[]? roles, CancellationToken ct)
    {
        if (roles is not { Length: > 0 })
            return await EnsureDefaultRoleAsync(ct);

        // ✅ Normalize input
        var normalizedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in roles)
        {
            if (!string.IsNullOrWhiteSpace(r))
                normalizedSet.Add(r.Trim().ToUpperInvariant());
        }

        if (normalizedSet.Count == 0)
            return await EnsureDefaultRoleAsync(ct);

        // ✅ Convert to List for EF Core compatibility
        var normalizedList = normalizedSet.ToList();

        // ⚡ LOAD ALL ROLES - NO WHERE CLAUSE
        var allRoles = await _db.Roles
            .AsNoTracking()
            .Select(r => new { r.Name, r.NormalizedName })
            .ToListAsync(ct);

        // ✅ Filter in-memory
        var matchedNames = allRoles
            .Where(r => normalizedList.Contains(r.NormalizedName!))
            .Select(r => r.Name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return matchedNames.Length == 0
            ? await EnsureDefaultRoleAsync(ct)
            : matchedNames;
    }
    private async Task<string> GenerateUniqueUserNameAsync(string email, CancellationToken ct)
    {
        var at = email.IndexOf('@');
        var local = (at > 0 ? email.Substring(0, at) : email).Trim();

        var baseName = Regex.Replace(local.ToLowerInvariant(), @"[^a-z0-9._-]", string.Empty);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "user";
        if (baseName.Length < 3)
        {
            var pad = baseName + "user";
            baseName = pad.Substring(0, Math.Min(16, pad.Length));
        }

        var candidate = baseName;
        var i = 0;
        while (await _users.FindByNameAsync(candidate) is not null)
        {
            i++;
            candidate = $"{baseName}{i}";
            if (i > 9999)
            {
                var guidPart = Guid.NewGuid().ToString("N");
                var combined = $"{baseName}{guidPart}";
                candidate = combined.Substring(0, Math.Min(24, baseName.Length + 8));
                break;
            }
        }
        return candidate;
    }

    // CORE thay roles (KHÔNG mở transaction)
    private async Task<Result> ReplaceRolesCoreAsync(User user, IEnumerable<string> roles, CancellationToken ct)
    {
        // Normalize input
        var normalizedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in roles)
        {
            if (!string.IsNullOrWhiteSpace(s))
                normalizedSet.Add(s.Trim().ToUpperInvariant());
        }

        // ✅ Convert to List for EF Core compatibility
        var normalizedList = normalizedSet.ToList();

        // ⚡ LOAD ALL ROLES
        var allRoles = await _db.Roles
            .AsNoTracking()
            .Select(r => new { r.Name, r.NormalizedName })
            .ToListAsync(ct);

        // ✅ Filter in-memory
        var validDesired = allRoles
            .Where(r => normalizedList.Contains(r.NormalizedName!))
            .Select(r => r.Name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var finalSet = new HashSet<string>(validDesired, StringComparer.OrdinalIgnoreCase);
        if (finalSet.Count == 0)
        {
            foreach (var d in await EnsureDefaultRoleAsync(ct))
                finalSet.Add(d);
        }

        var current = await _users.GetRolesAsync(user);
        var currentSet = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);

        var toRemove = current.Where(r => !finalSet.Contains(r)).ToArray();
        var toAdd = finalSet.Where(r => !currentSet.Contains(r)).ToArray();

        if (toRemove.Length > 0)
        {
            foreach (var roleName in toRemove)
            {
                var r1 = await _users.RemoveFromRoleAsync(user, roleName);
                if (!r1.Succeeded) return r1.ToResult($"Remove role '{roleName}' failed");
            }
        }
        if (toAdd.Length > 0)
        {
            var r2 = await AddRolesToUserAsync(user, toAdd);
            if (!r2.IsSuccess) return r2;
        }

        return Result.Success();
    }

    /// <summary>
    /// Add roles to user one by one to avoid EF Core translation issues with AddToRolesAsync
    /// </summary>
    private async Task<Result> AddRolesToUserAsync(User user, IEnumerable<string> roleNames)
    {
        foreach (var roleName in roleNames)
        {
            var result = await _users.AddToRoleAsync(user, roleName);
            if (!result.Succeeded)
                return result.ToResult($"Assign role '{roleName}' failed");
        }
        return Result.Success();
    }

    private async Task SendConfirmEmailIfNeededAsync(UserDetailDto dto, CancellationToken ct)
    {
        if (dto.EmailConfirmed ||
            string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(_authLinks.PublicBaseUrl))
            return;

        var user = await _users.FindByIdAsync(dto.Id.ToString());
        if (user is null) return;

        var token = await _users.GenerateEmailConfirmationTokenAsync(user);
        var path = string.IsNullOrWhiteSpace(_authLinks.ConfirmEmailPath)
            ? "auth/confirm-email"
            : _authLinks.ConfirmEmailPath;

        var url = BuildPublicLink(
            _authLinks.PublicBaseUrl,
            path,
            new()
            {
                ["uid"] = user.Id.ToString(),
                ["token"] = token.Base64UrlEncodeUtf8()
            });

        await _emails.EnqueueAsync(_authEmails.BuildConfirmEmail(user, url), ct);
    }
}
