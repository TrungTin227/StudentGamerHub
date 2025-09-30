using Microsoft.EntityFrameworkCore;

namespace BusinessObjects.Common.Pagination
{
    public static class OffsetPagingExtensions
    {
        /// <summary>
        /// Áp dụng sort theo tên property. Nếu không có sẽ fallback "Id".
        /// </summary>
        private static IQueryable<T> ApplySorting<T>(this IQueryable<T> q, string sort, bool desc)
        {
            // Nếu sort không tồn tại sẽ ném lỗi rõ ràng
            return q.OrderByProperty(sort, desc);
        }

        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
            this IQueryable<T> source,
            PageRequest request,
            CancellationToken ct = default)
        {
            var page = request.PageSafe;
            var size = request.SizeSafe;
            var sort = request.SortSafe;
            var desc = request.Desc;

            var query = source;
            query = query.ApplySorting(sort, desc);

            var total = await query.CountAsync(ct);
            var totalPages = (int)Math.Ceiling(total / (double)size);
            var hasPrev = page > 1;
            var hasNext = page < totalPages;

            var items = await query
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync(ct);

            return new PagedResult<T>(items, page, size, total, totalPages, hasPrev, hasNext, sort, desc);
        }
    }
}
