namespace DTOs.Bugs;

public sealed record BugReportStatusPatchRequest
{
    public string Status { get; init; } = default!;
}
