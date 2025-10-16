namespace Services.Common.Extensions
{
    /// <summary>
    /// Helpers làm rõ chủ trương dùng UTC trong DB, chỉ convert khi xuất DTO.
    /// </summary>
    public static class TimeExtensions
    {
        public static DateTime UtcNow() => DateTime.UtcNow;

        /// <summary>Convert UTC -> VN để hiển thị.</summary>
        public static DateTime ToVn(this ITimeZoneService tz, DateTime utc)
            => tz.ToVn(utc);
    }
}
