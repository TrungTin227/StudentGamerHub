namespace DTOs.Registrations;

public sealed record PaymentIntentDto(
    Guid Id,
    long AmountCents,
    PaymentPurpose Purpose,
    Guid? EventRegistrationId,
    PaymentIntentStatus Status,
    DateTime ExpiresAt,
    string ClientSecret);
