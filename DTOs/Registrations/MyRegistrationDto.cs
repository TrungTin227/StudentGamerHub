namespace DTOs.Registrations;

public sealed record MyRegistrationDto(
    Guid RegistrationId,
    Guid EventId,
    string EventTitle,
    DateTime StartsAt,
    string? Location,
    EventRegistrationStatus Status);
