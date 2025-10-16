namespace Services.Common.Extensions.Users
{
    public static class UserQueryExtensions
    {
        public static IQueryable<User> ApplyKeyword(this IQueryable<User> q, string? keyword)
        {
            var kw = keyword.NormalizeKeyword();
            if (kw is null) return q;

            return q.Where(u =>
                (u.NormalizedUserName ?? string.Empty).Contains(kw) ||
                (u.NormalizedEmail ?? string.Empty).Contains(kw) ||
                ((u.FullName ?? string.Empty).ToUpper()).Contains(kw));
        }

        public static IQueryable<User> ApplyFlags(this IQueryable<User> q, bool? emailConfirmed, bool? lockedOnlyUtc)
        {
            if (emailConfirmed.HasValue)
                q = q.Where(u => u.EmailConfirmed == emailConfirmed.Value);

            if (lockedOnlyUtc == true)
            {
                var now = DateTime.UtcNow;
                q = q.Where(u => u.LockoutEnd != null && u.LockoutEnd.Value.UtcDateTime > now);
            }
            return q;
        }

        /// <summary>
        /// Lọc theo khoảng thời gian UTC (DB lưu UTC, filter cũng là UTC).
        /// </summary>
        public static IQueryable<User> ApplyCreatedUtcRange(this IQueryable<User> q, DateTime? fromUtc, DateTime? toUtc)
        {
            if (fromUtc.HasValue) q = q.Where(u => u.CreatedAtUtc >= fromUtc.Value);
            if (toUtc.HasValue) q = q.Where(u => u.CreatedAtUtc <= toUtc.Value);
            return q;
        }

        public static IQueryable<User> ApplySort(this IQueryable<User> q, UserSortBy sortBy, bool desc)
        {
            return sortBy switch
            {
                UserSortBy.UserName => desc ? q.OrderByDescending(u => u.UserName) : q.OrderBy(u => u.UserName),
                UserSortBy.Email => desc ? q.OrderByDescending(u => u.Email) : q.OrderBy(u => u.Email),
                UserSortBy.FullName => desc ? q.OrderByDescending(u => u.FullName) : q.OrderBy(u => u.FullName),
                UserSortBy.UpdatedAt => desc ? q.OrderByDescending(u => u.UpdatedAtUtc) : q.OrderBy(u => u.UpdatedAtUtc),
                _ => desc ? q.OrderByDescending(u => u.CreatedAtUtc) : q.OrderBy(u => u.CreatedAtUtc),
            };
        }
    }
}
