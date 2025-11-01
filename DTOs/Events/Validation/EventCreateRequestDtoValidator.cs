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
            .GreaterThan(0);

        RuleFor(x => x.EndsAt)
            .Must((dto, endsAt) => endsAt is null || dto.StartsAt < endsAt)
            .WithMessage("EndsAt must be greater than StartsAt when provided.");
    }
}
