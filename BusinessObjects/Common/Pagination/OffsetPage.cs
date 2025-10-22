namespace BusinessObjects.Common.Pagination;

/// <summary>
/// Represents a single page of results when using offset-based pagination.
/// </summary>
/// <typeparam name="T">Type of the paged items.</typeparam>
public sealed record OffsetPage<T>(
    IReadOnlyList<T> Items,
    int Offset,
    int Limit,
    int TotalCount,
    bool HasPrevious,
    bool HasNext);
