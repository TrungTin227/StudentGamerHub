using System.Linq;

namespace BusinessObjects.Common.Pagination;

public static class OffsetPageExtensions
{
    /// <summary>
    /// Projects the items of an <see cref="OffsetPage{T}"/> into another type while preserving pagination metadata.
    /// </summary>
    public static OffsetPage<TResult> Map<TSource, TResult>(
        this OffsetPage<TSource> page,
        Func<TSource, TResult> selector)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(selector);

        var projected = page.Items
            .Select(selector)
            .ToList();

        return new OffsetPage<TResult>(
            projected,
            page.Offset,
            page.Limit,
            page.TotalCount,
            page.HasPrevious,
            page.HasNext);
    }
}
