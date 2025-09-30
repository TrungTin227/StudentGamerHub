using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace Services.Implementations
{
    public sealed class AuthService : IAuthService
    {
        private readonly UserManager<User> _users;
        private readonly SignInManager<User> _signIn;
        private readonly ITokenService _tokens;
        private readonly IValidator<LoginRequest> _loginValidator;
        private readonly IValidator<RefreshTokenRequest> _refreshValidator;
        private readonly IValidator<RevokeTokenRequest> _revokeValidator;

        public AuthService(
            UserManager<User> users,
            SignInManager<User> signIn,
            ITokenService tokens,
            IValidator<LoginRequest> loginValidator,
            IValidator<RefreshTokenRequest> refreshValidator,
            IValidator<RevokeTokenRequest> revokeValidator)
        {
            _users = users ?? throw new ArgumentNullException(nameof(users));
            _signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _loginValidator = loginValidator ?? throw new ArgumentNullException(nameof(loginValidator));
            _refreshValidator = refreshValidator ?? throw new ArgumentNullException(nameof(refreshValidator));
            _revokeValidator = revokeValidator ?? throw new ArgumentNullException(nameof(revokeValidator));
        }

        public async Task<Result<TokenPairDto>> LoginAsync(
            LoginRequest req,
            string? ip = null,
            string? userAgent = null,
            CancellationToken ct = default)
        {
            req = req.Normalize();

            return await _loginValidator.ValidateToResultAsync(req, ct)
                // Tìm user theo username/email
                .BindAsync(async _ =>
                {
                    var user = await FindByUserNameOrEmailAsync(req.UserNameOrEmail).ConfigureAwait(false);
                    return Result<User>.FromNullable(
                        user,
                        new Error(Error.Codes.Unauthorized, "Invalid credentials."));
                })
                // Yêu cầu xác nhận email nếu cấu hình bắt buộc
                .EnsureAsync(async user =>
                {
                    if (!_users.Options.SignIn.RequireConfirmedEmail)
                        return true;

                    return await _users.IsEmailConfirmedAsync(user).ConfigureAwait(false);
                }, new Error(Error.Codes.Forbidden, "Email is not confirmed."))
                // Kiểm tra password (kèm lockout/2FA)
                .BindAsync(async user =>
                {
                    var signIn = await _signIn.CheckPasswordSignInAsync(
                        user, req.Password, lockoutOnFailure: true).ConfigureAwait(false);

                    if (signIn.IsLockedOut)
                        return Result<User>.Failure(new Error(Error.Codes.Forbidden, "User is locked out."));

                    if (signIn.RequiresTwoFactor)
                        return Result<User>.Failure(new Error(Error.Codes.Unauthorized, "TwoFactorRequired"));

                    if (!signIn.Succeeded)
                        return Result<User>.Failure(new Error(Error.Codes.Unauthorized, "Invalid credentials."));

                    return Result<User>.Success(user);
                })
                // Phát hành token pair
                .BindAsync(user => ResultExtensions.TryAsync(async () =>
                {
                    var pair = await _tokens.IssueAsync(user, ip, userAgent, ct).ConfigureAwait(false);
                    return new TokenPairDto(
                        pair.AccessToken, pair.AccessExpiresAtUtc,
                        pair.RefreshToken, pair.RefreshExpiresAtUtc);
                }))
                .ConfigureAwait(false);
        }

        public async Task<Result<TokenPairDto>> RefreshAsync(
            RefreshTokenRequest req,
            string? ip = null,
            string? userAgent = null,
            CancellationToken ct = default)
        {
            req = req.Normalize();

            return await _refreshValidator.ValidateToResultAsync(req, ct)
                .BindAsync(_ => ResultExtensions.TryAsync(async () =>
                {
                    var pair = await _tokens.RefreshAsync(req.RefreshToken, ip ?? "unknown", userAgent, ct).ConfigureAwait(false);
                    return new TokenPairDto(
                        pair.AccessToken, pair.AccessExpiresAtUtc,
                        pair.RefreshToken, pair.RefreshExpiresAtUtc);
                },
                ex => ex is UnauthorizedAccessException
                    ? new Error(Error.Codes.Unauthorized, ex.Message)
                    : new Error(Error.Codes.Unexpected, ex.Message)))
                .ConfigureAwait(false);
        }

        public async Task<Result> RevokeAsync(
            RevokeTokenRequest req,
            string? ip = null,
            CancellationToken ct = default)
        {
            req = req.Normalize();

            var r = await _revokeValidator.ValidateToResultAsync(req, ct)
                .BindAsync(_ => ResultExtensions.TryAsync(async () =>
                {
                    await _tokens.RevokeAsync(req.RefreshToken, ip ?? "unknown", ct: ct).ConfigureAwait(false);
                    return true; // giá trị placeholder, sẽ bỏ bằng ToResult()
                },
                ex => new Error(Error.Codes.Unexpected, ex.Message)))
                .ConfigureAwait(false);

            return r.ToResult(); // Result<bool> -> Result
        }

        // Overloads tương thích cũ (nếu muốn giữ)
        public Task<Result<TokenPairDto>> RefreshAsync(string refreshToken, string? ip = null, string? userAgent = null, CancellationToken ct = default)
            => RefreshAsync(new RefreshTokenRequest { RefreshToken = refreshToken }, ip, userAgent, ct);

        public Task<Result> RevokeAsync(string refreshToken, string? ip = null, CancellationToken ct = default)
            => RevokeAsync(new RevokeTokenRequest { RefreshToken = refreshToken }, ip, ct);

        // ---- Helpers ----
        private async Task<User?> FindByUserNameOrEmailAsync(string userNameOrEmail)
        {
            var byEmail = await _users.FindByEmailAsync(userNameOrEmail).ConfigureAwait(false);
            if (byEmail is not null) return byEmail;

            var byName = await _users.FindByNameAsync(userNameOrEmail).ConfigureAwait(false);
            return byName;
        }
    }
}
