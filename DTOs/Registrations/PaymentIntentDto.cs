namespace DTOs.Registrations;

public sealed record PaymentIntentDto(
    Guid Id,
    long AmountCents,
    PaymentPurpose Purpose,
    Guid? EventRegistrationId,
    Guid? EventId,
    PaymentIntentStatus Status,
    DateTime ExpiresAt,
    string ClientSecret,
    string? ProviderName,
    string? TransactionId,
    string? MetadataJson,
    long OrderCode,
    DateTime CreatedAt);
