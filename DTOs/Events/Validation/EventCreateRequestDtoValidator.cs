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
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.EscrowMinCents)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.PlatformFeeRate)
            .InclusiveBetween(0m, 1m);

        RuleFor(x => x.EndsAt)
            .Must((dto, endsAt) => endsAt is null || dto.StartsAt < endsAt)
            .WithMessage("EndsAt must be greater than StartsAt when provided.");
    }
}
