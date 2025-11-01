namespace DTOs.Events;

public sealed record EventCreateRequestDto(
    Guid? CommunityId,
    string Title,
    string? Description,
    EventMode Mode,
    string? Location,
    DateTime StartsAt,
    DateTime? EndsAt,
    long PriceCents,
    int? Capacity);
