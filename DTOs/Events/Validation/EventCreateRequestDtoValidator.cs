using FluentValidation;

namespace DTOs.Events.Validation;

public sealed class EventCreateRequestDtoValidator : AbstractValidator<EventCreateRequestDto>
{
    public EventCreateRequestDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.PriceCents)
            .GreaterThanOrEqualTo(0)
            .When(x => x.PriceCents.HasValue)
            .WithMessage("PriceCents must be greater than or equal to zero when provided.");

        RuleFor(x => x.EndsAt)
            .Must((dto, endsAt) => endsAt is null || dto.StartsAt < endsAt)
            .WithMessage("EndsAt must be greater than StartsAt when provided.");
    }
}
