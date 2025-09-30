namespace Services.Common.Extensions
{
    /// <summary>
    /// Helpers làm rõ chủ trương dùng UTC trong DB, chỉ convert khi xuất DTO.
    /// </summary>
    public static class TimeExtensions
    {
        public static DateTime UtcNow() => DateTime.UtcNow;

        public static DateTimeOffset UtcNowOffset() => DateTimeOffset.UtcNow;

        /// <summary>Convert UTC -> VN để hiển thị.</summary>
        public static DateTime ToVn(this ITimeZoneService tz, DateTime utc)
            => tz.ToVn(utc);

        /// <summary>Convert UTC -> VN để hiển thị.</summary>
        public static DateTimeOffset ToVn(this ITimeZoneService tz, DateTimeOffset utc)
            => tz.ToVn(utc);
    }
}
