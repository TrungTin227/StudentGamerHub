namespace DTOs.Registrations;

public sealed record RegistrationListItemDto(
    Guid Id,
    Guid EventId,
    Guid UserId,
    EventRegistrationStatus Status,
    Guid? PaidTransactionId,
    DateTimeOffset CreatedAt);
