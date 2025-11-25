namespace DTOs.Dashboard;

/// <summary>
/// Brief event information for Dashboard
/// </summary>
public sealed record EventBriefDto(
    Guid Id,
    string Title,
    DateTime StartsAt,
    DateTime? EndsAt,
    string? Location,
    string Mode,
    string Status,
    int RegisteredCount,
    int ConfirmedCount
);
