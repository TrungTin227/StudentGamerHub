namespace DTOs.Events;

/// <summary>
/// Request DTO for updating an existing event.
/// Allows organizer to update event details including community association.
/// </summary>
public sealed record EventUpdateRequestDto(
    Guid? CommunityId,
    string? Title,
    string? Description,
    EventMode? Mode,
    string? Location,
    DateTime? StartsAt,
    DateTime? EndsAt,
    long? PriceCents,
    int? Capacity);
