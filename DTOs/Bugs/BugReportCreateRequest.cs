namespace DTOs.Bugs;

public sealed record BugReportCreateRequest
{
    public string Category { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string? ImageUrl { get; init; }
}
