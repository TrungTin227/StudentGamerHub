namespace DTOs.Events;

public sealed record EventCreateRequestDto(
    Guid? CommunityId,
    string Title,
    string? Description,
    EventMode Mode,
    string? Location,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    long PriceCents,
    int? Capacity,
    long EscrowMinCents,
    decimal PlatformFeeRate,
    GatewayFeePolicy GatewayFeePolicy);
