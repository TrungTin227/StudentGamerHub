namespace Repositories.Persistence;

public sealed class SeedOptions
{
    // Bật/tắt các bước
    public bool ApplyMigrations { get; set; } = true;
    public bool Run { get; set; } = true;             // có seed hay không
    public bool AllowInProduction { get; set; } = false;

    // Danh sách role
    public string[] Roles { get; set; } = new[] { "Admin", "User" };

    // Admin
    public AdminOptions Admin { get; set; } = new();

    // Comprehensive seeding options
    public ComprehensiveSeedOptions ComprehensiveSeeding { get; set; } = new();

    public sealed class AdminOptions
    {
        public string Email { get; set; } = "tinvtse@gmail.com";
        public string? Password { get; set; } = "Admin@123";         // để null => không tạo/đổi pass
        public string FullName { get; set; } = "Administrator";

        // true => reset mật khẩu về Password (nếu cung cấp)
        public bool ForceResetPassword { get; set; } = false;

        // true => luôn đảm bảo user nằm trong role "Admin" nếu role tồn tại
        public bool EnsureAdminRole { get; set; } = true;
    }

    public sealed class ComprehensiveSeedOptions
    {
        public bool SeedSampleUsers { get; set; } = true;
        public bool SeedCommunities { get; set; } = true;
        public bool SeedClubsAndRooms { get; set; } = true;
        public bool SeedEvents { get; set; } = true;
        public bool SeedWalletsAndTransactions { get; set; } = true;
        public bool SeedFriendships { get; set; } = true;
        public bool SeedGifts { get; set; } = true;
        public bool SeedBugReports { get; set; } = true;

        public int MaxSampleUsers { get; set; } = 20;
        public int MaxCommunities { get; set; } = 10;
        public int MaxEvents { get; set; } = 15;
        public int MaxBugReports { get; set; } = 20;
        public int MaxFriendLinks { get; set; } = 30;
    }
}
