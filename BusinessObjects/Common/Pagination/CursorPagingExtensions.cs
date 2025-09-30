using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text.Json;

namespace BusinessObjects.Common.Pagination
{
    public static class CursorPagingExtensions
    {
        // Token chứa "key" của phần tử đầu/cuối trang + chiều sort hiện tại
        private sealed record CursorToken<TKey>(TKey Key, bool Desc);

        private static string EncodeToken<TKey>(CursorToken<TKey> token)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(token);
            return Convert.ToBase64String(bytes);
        }

        private static bool TryDecodeToken<TKey>(string? token, out CursorToken<TKey>? result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(token)) return false;
            try
            {
                var bytes = Convert.FromBase64String(token);
                result = JsonSerializer.Deserialize<CursorToken<TKey>>(bytes);
                return result is not null;
            }
            catch { return false; }
        }

        /// <summary>
        /// Tạo biểu thức so sánh key thông qua Comparer&lt;TKey&gt; để hỗ trợ mọi kiểu (Guid, DateTime, long, string...).
        /// </summary>
        private static Expression<Func<T, bool>> BuildComparePredicate<T, TKey>(
            Expression<Func<T, TKey>> keySelector,
            TKey pivot,
            bool desc,
            CursorDirection direction)
        {
            // Quy ước:
            // - Sort ASC (desc == false): Next => key > pivot, Prev => key < pivot
            // - Sort DESC (desc == true):  Next => key < pivot, Prev => key > pivot
            var param = keySelector.Parameters[0];
            var left = keySelector.Body;
            var pivotConst = Expression.Constant(pivot, typeof(TKey));

            // Comparer<TKey>.Default.Compare(left, pivot) > 0
            var comparerDefault = Expression.Property(null, typeof(Comparer<TKey>), nameof(Comparer<TKey>.Default));
            var compareMethod = typeof(Comparer<TKey>).GetMethod(nameof(Comparer<TKey>.Compare), new[] { typeof(TKey), typeof(TKey) })!;
            var compareCall = Expression.Call(comparerDefault, compareMethod, left, pivotConst);
            var zero = Expression.Constant(0);

            bool isGreater;
            if (!desc)
            {
                isGreater = direction == CursorDirection.Next; // asc + next => >
            }
            else
            {
                isGreater = direction == CursorDirection.Prev; // desc + prev => >
            }

            var cmp = isGreater ? Expression.GreaterThan(compareCall, zero)
                                : Expression.LessThan(compareCall, zero);

            return Expression.Lambda<Func<T, bool>>(cmp, param);
        }

        /// <summary>
        /// Cursor paging: luôn đảm bảo thứ tự ổn định theo keySelector.
        /// Hỗ trợ Next/Prev. Prev sẽ query theo chiều ngược rồi đảo lại.
        /// </summary>
        public static async Task<CursorPageResult<T>> ToCursorPageAsync<T, TKey>(
            this IQueryable<T> source,
            CursorRequest request,
            Expression<Func<T, TKey>> keySelector,
            CancellationToken ct = default)
        {
            var size = request.SizeSafe;
            var desc = request.Desc;

            // 1) Base sort theo key
            IOrderedQueryable<T> ordered = desc
                ? source.OrderByProperty(request.SortSafe, true)
                : source.OrderByProperty(request.SortSafe, false);

            // 2) Nếu có cursor → lọc phía trước/ sau pivot
            if (TryDecodeToken<TKey>(request.Cursor, out var token) && token is not null)
            {
                if (token.Desc != desc)
                    throw new InvalidOperationException("Cursor không khớp hướng sort hiện tại (Desc).");

                var predicate = BuildComparePredicate(keySelector, token.Key, desc, request.Direction);
                ordered = (IOrderedQueryable<T>)ordered.Where(predicate);
            }

            // 3) Nếu prev → đảo chiều để lấy "trang trước" rồi đảo lại sau khi lấy items
            bool reverseAfter = false;
            if (request.Direction == CursorDirection.Prev)
            {
                ordered = desc
                    ? ordered.ThenByProperty(request.SortSafe, false) as IOrderedQueryable<T> ?? ordered
                    : ordered.ThenByProperty(request.SortSafe, true) as IOrderedQueryable<T> ?? ordered;

                // Cách đơn giản: đảo chiều sort chính
                ordered = desc
                    ? source.OrderByProperty(request.SortSafe, false)
                    : source.OrderByProperty(request.SortSafe, true);

                if (TryDecodeToken<TKey>(request.Cursor, out var token2) && token2 is not null)
                {
                    var pred = BuildComparePredicate(keySelector, token2.Key, !desc, CursorDirection.Next);
                    ordered = (IOrderedQueryable<T>)ordered.Where(pred);
                }

                reverseAfter = true;
            }

            // 4) Lấy (size + 1) để biết còn trang sau không
            var pageItems = await ordered
                .Take(size + 1)
                .ToListAsync(ct);

            var hasMore = pageItems.Count > size;
            if (hasMore) pageItems.RemoveAt(pageItems.Count - 1);

            if (reverseAfter)
                pageItems.Reverse();

            // 5) Tạo next/prev cursor từ phần tử đầu/cuối
            string? nextCursor = null;
            string? prevCursor = null;

            if (pageItems.Count > 0)
            {
                var firstKey = keySelector.Compile().Invoke(pageItems.First());
                var lastKey = keySelector.Compile().Invoke(pageItems.Last());

                // Với trang hiện tại:
                // - Next cursor lấy từ "lastKey" theo hướng hiện tại.
                // - Prev cursor lấy từ "firstKey".
                nextCursor = hasMore ? EncodeToken(new CursorToken<TKey>(lastKey, desc)) : null;
                prevCursor = EncodeToken(new CursorToken<TKey>(firstKey, desc));
            }

            return new CursorPageResult<T>(
                Items: pageItems,
                NextCursor: nextCursor,
                PrevCursor: prevCursor,
                Size: size,
                Sort: request.SortSafe,
                Desc: desc
            );
        }
    }
}