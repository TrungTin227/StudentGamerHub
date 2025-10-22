using DTOs.Bugs;
using FluentValidation;

namespace Services.Validation.Bugs;

public sealed class BugReportCreateValidator : AbstractValidator<BugReportCreateRequest>
{
    public BugReportCreateValidator()
    {
        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(64).WithMessage("Category cannot exceed 64 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(4000).WithMessage("Description cannot exceed 4000 characters.");

        When(x => !string.IsNullOrWhiteSpace(x.ImageUrl), () =>
        {
            RuleFor(x => x.ImageUrl!)
                .MaximumLength(1024).WithMessage("ImageUrl cannot exceed 1024 characters.");
        });
    }
}

public sealed class BugReportStatusPatchValidator : AbstractValidator<BugReportStatusPatchRequest>
{
    private static readonly string[] AllowedStatuses = { "Open", "InProgress", "Resolved", "Rejected" };

    public BugReportStatusPatchValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => AllowedStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Status must be one of: {string.Join(", ", AllowedStatuses)}");
    }
}
