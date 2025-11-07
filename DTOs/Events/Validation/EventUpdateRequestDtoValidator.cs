using FluentValidation;

namespace DTOs.Events.Validation;

public sealed class EventUpdateRequestDtoValidator : AbstractValidator<EventUpdateRequestDto>
{
    public EventUpdateRequestDtoValidator()
    {
        When(x => x.Title is not null, () =>
     {
 RuleFor(x => x.Title!)
.NotEmpty()
         .WithMessage("Title cannot be empty when provided.")
          .MaximumLength(256)
           .WithMessage("Title must not exceed 256 characters.");
        });

        When(x => x.Description is not null, () =>
        {
          RuleFor(x => x.Description!)
           .MaximumLength(2048)
       .WithMessage("Description must not exceed 2048 characters.");
        });

        When(x => x.Location is not null, () =>
        {
          RuleFor(x => x.Location!)
       .MaximumLength(512)
 .WithMessage("Location must not exceed 512 characters.");
        });

When(x => x.StartsAt.HasValue, () =>
        {
   RuleFor(x => x.StartsAt!.Value)
      .GreaterThanOrEqualTo(DateTime.UtcNow)
           .WithMessage("StartsAt must be in the future when provided.");
        });

     When(x => x.EndsAt.HasValue && x.StartsAt.HasValue, () =>
        {
            RuleFor(x => x.EndsAt!.Value)
     .GreaterThan(x => x.StartsAt!.Value)
     .WithMessage("EndsAt must be after StartsAt when both are provided.");
        });

        When(x => x.PriceCents.HasValue, () =>
     {
            RuleFor(x => x.PriceCents!.Value)
     .GreaterThanOrEqualTo(0)
                .WithMessage("PriceCents must be non-negative when provided.");
  });

        When(x => x.Capacity.HasValue, () =>
        {
            RuleFor(x => x.Capacity!.Value)
          .GreaterThan(0)
                .WithMessage("Capacity must be greater than zero when provided.");
        });
    }
}
