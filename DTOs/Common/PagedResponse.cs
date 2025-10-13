using BusinessObjects.Common.Pagination;

namespace DTOs.Common;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPrevious,
    bool HasNext,
    string Sort,
    bool Desc)
{
    public static PagedResponse<T> FromPagedResult(PagedResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new PagedResponse<T>(
            result.Items,
            result.Page,
            result.Size,
            result.TotalCount,
            result.TotalPages,
            result.HasPrevious,
            result.HasNext,
            result.Sort,
            result.Desc);
    }
}
