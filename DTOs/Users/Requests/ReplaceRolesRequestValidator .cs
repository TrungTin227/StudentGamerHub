using FluentValidation;

namespace DTOs.Users.Requests
{

        public sealed class ReplaceRolesRequestValidator : AbstractValidator<ReplaceRolesRequest>
        {
            public ReplaceRolesRequestValidator()
            {
                RuleFor(x => x.Roles)
                    .NotNull().WithMessage("Roles is required")
                    .Must(r => r!.Any()).WithMessage("Roles must not be empty");
            }
        }
    
}
