using FluentValidation;
using System.Text.RegularExpressions;

namespace DTOs.Users.Validation
{
    internal static class ValidationHelpers
    {
        internal static bool IsPhoneLike(string phone)
            => Regex.IsMatch(phone, @"^[\+\d\-\s\(\)]{7,20}$");
        internal static bool BeValidUrl(string url)
            => Uri.TryCreate(url, UriKind.Absolute, out _);
    }

    // ---------- Create / Register / Update ----------
    public sealed class CreateUserAdminRequestValidator : AbstractValidator<CreateUserAdminRequest>
    {
        public CreateUserAdminRequestValidator()
        {
            RuleFor(x => x.UserName)
                .NotEmpty().MaximumLength(256)
                .Must(u => !u.Contains(' ')).WithMessage("UserName không được chứa khoảng trắng.");

            RuleFor(x => x.Email)
                .NotEmpty().EmailAddress().MaximumLength(256);

            RuleFor(x => x.Password)
                .NotEmpty().MinimumLength(6);

            When(x => !string.IsNullOrWhiteSpace(x.FullName), () =>
                RuleFor(x => x.FullName!).MaximumLength(256));

            When(x => !string.IsNullOrWhiteSpace(x.University), () =>
                RuleFor(x => x.University!).MaximumLength(256));

            When(x => x.Level.HasValue, () =>
                RuleFor(x => x.Level!.Value).GreaterThanOrEqualTo(1));

            When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber), () =>
                RuleFor(x => x.PhoneNumber!).Must(ValidationHelpers.IsPhoneLike).MaximumLength(32));

            When(x => !string.IsNullOrWhiteSpace(x.AvatarUrl), () =>
                RuleFor(x => x.AvatarUrl!).Must(ValidationHelpers.BeValidUrl));

            When(x => !string.IsNullOrWhiteSpace(x.CoverUrl), () =>
                RuleFor(x => x.CoverUrl!).Must(ValidationHelpers.BeValidUrl));

            When(x => x.Roles is { Length: > 0 }, () =>
            {
                RuleFor(x => x.Roles!)
                    .Must(r => r.All(s => !string.IsNullOrWhiteSpace(s))).WithMessage("Roles chứa phần tử rỗng.")
                    .Must(r => r.Distinct(StringComparer.OrdinalIgnoreCase).Count() == r.Length)
                    .WithMessage("Roles bị trùng lặp (không phân biệt hoa thường).");
            });
        }
    }

    public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
    {
        public RegisterRequestValidator()
        {
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(256);
            RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
            RuleFor(x => x.Password).NotEmpty().MinimumLength(6);

            When(x => !string.IsNullOrWhiteSpace(x.University), () =>
                RuleFor(x => x.University!).MaximumLength(256));

            When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber), () =>
                RuleFor(x => x.PhoneNumber!).Must(ValidationHelpers.IsPhoneLike).MaximumLength(32));
        }
    }

    public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
    {
        public UpdateUserRequestValidator()
        {
            When(x => !string.IsNullOrWhiteSpace(x.FullName), () =>
                RuleFor(x => x.FullName!).MaximumLength(256));

            When(x => !string.IsNullOrWhiteSpace(x.University), () =>
                RuleFor(x => x.University!).MaximumLength(256));

            When(x => x.Level.HasValue, () =>
                RuleFor(x => x.Level!.Value).GreaterThanOrEqualTo(1));

            When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber), () =>
                RuleFor(x => x.PhoneNumber!).Must(ValidationHelpers.IsPhoneLike).MaximumLength(32));

            When(x => !string.IsNullOrWhiteSpace(x.AvatarUrl), () =>
                RuleFor(x => x.AvatarUrl!).Must(ValidationHelpers.BeValidUrl));

            When(x => !string.IsNullOrWhiteSpace(x.CoverUrl), () =>
                RuleFor(x => x.CoverUrl!).Must(ValidationHelpers.BeValidUrl));

            When(x => x.ReplaceRoles is { Length: > 0 }, () =>
            {
                RuleFor(x => x.ReplaceRoles!)
                    .Must(r => r.All(s => !string.IsNullOrWhiteSpace(s))).WithMessage("ReplaceRoles chứa phần tử rỗng.")
                    .Must(r => r.Distinct(StringComparer.OrdinalIgnoreCase).Count() == r.Length)
                    .WithMessage("ReplaceRoles bị trùng lặp (không phân biệt hoa thường).");
            });
        }
    }

    // ---------- Roles ----------
    public sealed class ReplaceRolesRequestValidator : AbstractValidator<ReplaceRolesRequest>
    {
        public ReplaceRolesRequestValidator()
        {
            RuleFor(x => x.Roles)
                .NotNull().WithMessage("Roles là bắt buộc.")
                .Must(r => r!.Any()).WithMessage("Roles không được rỗng.")
                .Must(r => r!.All(s => !string.IsNullOrWhiteSpace(s))).WithMessage("Roles chứa phần tử rỗng.")
                .Must(r => r!.Distinct(StringComparer.OrdinalIgnoreCase).Count() == r!.Count())
                .WithMessage("Roles bị trùng lặp (không phân biệt hoa thường).");
        }
    }

    public sealed class ModifyRolesRequestValidator : AbstractValidator<ModifyRolesRequest>
    {
        public ModifyRolesRequestValidator()
        {
            RuleFor(x => x)
                .Must(x =>
                {
                    bool hasAdd = x.Add is { Length: > 0 } && x.Add.Any(s => !string.IsNullOrWhiteSpace(s));
                    bool hasRemove = x.Remove is { Length: > 0 } && x.Remove.Any(s => !string.IsNullOrWhiteSpace(s));
                    return hasAdd || hasRemove;
                }).WithMessage("Cần ít nhất một trong Add/Remove có giá trị.");

            When(x => x.Add is { Length: > 0 }, () =>
            {
                RuleForEach(x => x.Add!).NotEmpty().WithMessage("Add chứa phần tử rỗng.");
                RuleFor(x => x.Add!)
                    .Must(r => r.Distinct(StringComparer.OrdinalIgnoreCase).Count() == r.Length)
                    .WithMessage("Add có phần tử trùng lặp (không phân biệt hoa thường).");
            });

            When(x => x.Remove is { Length: > 0 }, () =>
            {
                RuleForEach(x => x.Remove!).NotEmpty().WithMessage("Remove chứa phần tử rỗng.");
                RuleFor(x => x.Remove!)
                    .Must(r => r.Distinct(StringComparer.OrdinalIgnoreCase).Count() == r.Length)
                    .WithMessage("Remove có phần tử trùng lặp (không phân biệt hoa thường).");
            });
        }
    }

    // ---------- Password / Email / Lockout ----------
    public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
    {
        public ChangePasswordRequestValidator()
        {
            RuleFor(x => x.CurrentPassword).NotEmpty();
            RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
            RuleFor(x => x).Must(x => x.CurrentPassword != x.NewPassword)
                .WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại.");
        }
    }

    public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
    {
        public ForgotPasswordRequestValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }

    public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
    {
        public ResetPasswordRequestValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.Token).NotEmpty();
            RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
        }
    }

    public sealed class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
    {
        public ConfirmEmailRequestValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.Token).NotEmpty();
        }
    }

    public sealed class ChangeEmailRequestValidator : AbstractValidator<ChangeEmailRequest>
    {
        public ChangeEmailRequestValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.NewEmail).NotEmpty().EmailAddress();
        }
    }

    public sealed class ConfirmChangeEmailRequestValidator : AbstractValidator<ConfirmChangeEmailRequest>
    {
        public ConfirmChangeEmailRequestValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.NewEmail).NotEmpty().EmailAddress();
            RuleFor(x => x.Token).NotEmpty();
        }
    }

    public sealed class SetLockoutRequestValidator : AbstractValidator<SetLockoutRequest>
    {
        public SetLockoutRequestValidator()
        {
            When(x => x.Enable && x.Minutes.HasValue, () =>
                RuleFor(x => x.Minutes!.Value).GreaterThan(0));
        }
    }

    // ---------- Filters ----------
    public sealed class UserFilterValidator : AbstractValidator<UserFilter>
    {
        public UserFilterValidator()
        {
            When(x => x.CreatedFromUtc.HasValue && x.CreatedToUtc.HasValue, () =>
                RuleFor(x => x).Must(x => x.CreatedFromUtc!.Value <= x.CreatedToUtc!.Value)
                    .WithMessage("CreatedFromUtc phải ≤ CreatedToUtc."));

            When(x => x.Roles is { Length: > 0 }, () =>
                RuleFor(x => x.Roles!).Must(r => r.All(s => !string.IsNullOrWhiteSpace(s)))
                    .WithMessage("Roles filter chứa phần tử rỗng."));

            When(x => x.LevelMin.HasValue && x.LevelMax.HasValue, () =>
                RuleFor(x => x).Must(x => x.LevelMin! <= x.LevelMax!)
                    .WithMessage("LevelMin phải ≤ LevelMax."));
        }
    }
}
