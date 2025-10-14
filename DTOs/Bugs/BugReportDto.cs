namespace DTOs.Bugs;

public sealed record BugReportDto(
    Guid Id,
    Guid UserId,
    string Category,
    string Description,
    string? ImageUrl,
    string Status,
    DateTime CreatedAtUtc
);
