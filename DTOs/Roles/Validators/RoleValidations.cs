using FluentValidation;
using System.Text.RegularExpressions;

namespace DTOs.Roles.Validators
{
    public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
    {
        private const int NameMaxLen = 256;
        private const int DescMaxLen = 512;

        private readonly Regex _safeNameRegex =
            new Regex(@"^[A-Za-z0-9\-_.:\s]+$", RegexOptions.CultureInvariant);

        public CreateRoleRequestValidator()
        {
            RuleLevelCascadeMode = CascadeMode.Stop;

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .Must(n => !string.IsNullOrWhiteSpace(n))
                    .WithMessage("Name must not be whitespace.")
                .Custom((n, ctx) =>
                {
                    if (n is null) return;
                    if (n.Trim().Length > NameMaxLen)
                        ctx.AddFailure(nameof(CreateRoleRequest.Name),
                            $"Name (trimmed) must be at most {NameMaxLen} characters.");
                })
                .Must(n => n is not null && _safeNameRegex.IsMatch(n))
                    .WithMessage("Name may only contain letters, digits, spaces, '-', '_', '.', ':'.");

            RuleFor(x => x.Description)
                .Must(d => d == null || d.Trim().Length <= DescMaxLen)
                .WithMessage($"Description must be at most {DescMaxLen} characters.");
        }
    }

    public sealed partial class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
    {
        private const int NameMaxLen = 256;
        private const int DescMaxLen = 512;

        // Source-generated regex (an toàn, hiệu năng tốt, không lỗi type initializer)
        [GeneratedRegex(@"^[A-Za-z0-9\-_.:\s]+$", RegexOptions.CultureInvariant)]
        private static partial Regex SafeNameRegex();

        public UpdateRoleRequestValidator()
        {
            RuleLevelCascadeMode = CascadeMode.Stop;

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .Must(n => !string.IsNullOrWhiteSpace(n))
                    .WithMessage("Name must not be whitespace.")
                // kiểm tra độ dài sau khi Trim
                .Custom((n, ctx) =>
                {
                    if (n is null) return;
                    if (n.Trim().Length > NameMaxLen)
                        ctx.AddFailure(nameof(UpdateRoleRequest.Name),
                            $"Name (trimmed) must be at most {NameMaxLen} characters.");
                })
                // whitelist ký tự
                .Must(n => n is not null && SafeNameRegex().IsMatch(n))
                    .WithMessage("Name may only contain letters, digits, spaces, '-', '_', '.', ':'.");

            RuleFor(x => x.Description)
                .Must(d => d == null || d.Trim().Length <= DescMaxLen)
                .WithMessage($"Description must be at most {DescMaxLen} characters.");
        }
    }
}
