using FluentValidation;

namespace DTOs.Auth.Validation
{
    public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.UserNameOrEmail)
                .NotEmpty().WithMessage("Tên đăng nhập hoặc email là bắt buộc.")
                .MaximumLength(256); // đồng bộ với DataAnnotations

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Mật khẩu là bắt buộc.");

            // Nếu có nhập 2FA thì chỉ được nhập một loại
            When(x => !string.IsNullOrWhiteSpace(x.TwoFactorCode) || !string.IsNullOrWhiteSpace(x.TwoFactorRecoveryCode), () =>
            {
                RuleFor(x => x)
                    .Must(x => string.IsNullOrWhiteSpace(x.TwoFactorCode) ^ string.IsNullOrWhiteSpace(x.TwoFactorRecoveryCode))
                    .WithMessage("Chỉ được cung cấp một trong TwoFactorCode hoặc TwoFactorRecoveryCode.");

                When(x => !string.IsNullOrWhiteSpace(x.TwoFactorCode), () =>
                    RuleFor(x => x.TwoFactorCode!)
                        .Matches(@"^\d{6}$").WithMessage("TwoFactorCode phải gồm 6 chữ số."));

                When(x => !string.IsNullOrWhiteSpace(x.TwoFactorRecoveryCode), () =>
                    RuleFor(x => x.TwoFactorRecoveryCode!)
                        .MaximumLength(50)); // tuỳ provider (MS dùng dạng 8/10 ký tự, có thể có '-')
            });
        }
    }

    public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
    {
        public RefreshTokenRequestValidator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty().WithMessage("Thiếu refresh token.")
                .MaximumLength(4096);
        }
    }

    public sealed class RevokeTokenRequestValidator : AbstractValidator<RevokeTokenRequest>
    {
        public RevokeTokenRequestValidator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty().WithMessage("Thiếu refresh token.")
                .MaximumLength(4096);
        }
    }

    public sealed class GoogleLoginRequestValidator : AbstractValidator<DTOs.Auth.Requests.GoogleLoginRequest>
    {
        public GoogleLoginRequestValidator()
        {
            RuleFor(x => x.IdToken)
                .NotEmpty().WithMessage("IdToken là bắt buộc.")
                .MaximumLength(4096);
        }
    }
}
