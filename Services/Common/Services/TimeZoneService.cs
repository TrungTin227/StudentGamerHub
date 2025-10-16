namespace Services.Common.Services
{
    public sealed class TimeZoneService : ITimeZoneService
    {
        private readonly TimeZoneInfo _vn;

        public TimeZoneService() => _vn = GetVnTz();

        public DateTime ToVn(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _vn);

        // --- Helper: lấy TimeZone VN phù hợp theo OS, có fallback ---
        private static TimeZoneInfo GetVnTz()
        {
            // Windows dùng "SE Asia Standard Time"
            // Linux/macOS dùng "Asia/Ho_Chi_Minh" (có nơi cũ còn "Asia/Saigon")
            var candidates = OperatingSystem.IsWindows()
                ? new[] { "SE Asia Standard Time" }
                : new[] { "Asia/Ho_Chi_Minh", "Asia/Saigon" };

            foreach (var id in candidates)
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch { /* thử id tiếp theo */ }
            }

            // Fallback: UTC (ít nhất không ném lỗi)
            return TimeZoneInfo.Utc;
        }
    }
}
