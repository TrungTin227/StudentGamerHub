namespace DTOs.Dashboard;

/// <summary>
/// Brief event information for Dashboard
/// </summary>
public sealed record EventBriefDto(
    Guid Id,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string? Location,
    string Mode
);
