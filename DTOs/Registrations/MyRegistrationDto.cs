namespace DTOs.Registrations;

public sealed record MyRegistrationDto(
    Guid RegistrationId,
    Guid EventId,
    string EventTitle,
    DateTimeOffset StartsAt,
    string? Location,
    EventRegistrationStatus Status);
