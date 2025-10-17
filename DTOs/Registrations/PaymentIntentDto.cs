namespace DTOs.Registrations;

public sealed record PaymentIntentDto(
    Guid Id,
    long AmountCents,
    PaymentPurpose Purpose,
    Guid? EventRegistrationId,
    Guid? EventId,
    PaymentIntentStatus Status,
    DateTime ExpiresAt,
    string ClientSecret);
