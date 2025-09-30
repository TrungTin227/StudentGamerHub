using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Persistence
{
    /// <summary>
    /// AppDbContext cho PostgreSQL (Npgsql).
    /// - Identity với User/Role GUID
    /// - Soft-delete + Audit stamps
    /// - Gender (enum) được lưu dưới DB dạng string
    /// - Partial unique index để tránh trùng khi IsDeleted = true
    /// </summary>
    public sealed class AppDbContext
        : IdentityDbContext<User, Role, Guid, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // DbSets (tiện query)
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<UserClaim> UserClaims => Set<UserClaim>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();
        public DbSet<UserLogin> UserLogins => Set<UserLogin>();
        public DbSet<RoleClaim> RoleClaims => Set<RoleClaim>();
        public DbSet<UserToken> UserTokens => Set<UserToken>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<NotificationPrefs> NotificationPrefs => Set<NotificationPrefs>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            // ---------- Tên bảng (Postgres: thường dùng snake_case hoặc lowercase; giữ nguyên nếu bạn muốn)
            b.Entity<User>().ToTable("users");
            b.Entity<Role>().ToTable("roles");
            b.Entity<UserClaim>().ToTable("user_claims");
            b.Entity<UserRole>().ToTable("user_roles");
            b.Entity<UserLogin>().ToTable("user_logins");
            b.Entity<RoleClaim>().ToTable("role_claims");
            b.Entity<UserToken>().ToTable("user_tokens");
            b.Entity<RefreshToken>().ToTable("refresh_tokens");
            b.Entity<NotificationPrefs>().ToTable("notification_prefs");

            // ---------- USER
            b.Entity<User>(e =>
            {
                // Identity core fields
                e.Property(u => u.UserName).HasMaxLength(256);
                e.Property(u => u.NormalizedUserName).HasMaxLength(256);
                e.Property(u => u.Email).HasMaxLength(256);
                e.Property(u => u.NormalizedEmail).HasMaxLength(256);

                // Profile extras
                e.Property(u => u.FullName).HasMaxLength(256);
                e.Property(u => u.University).HasMaxLength(256);

                // Map Gender enum -> string (PostgreSQL lưu text)
                e.Property(u => u.Gender)
                    .HasConversion<string>()     // LƯU STRING DƯỚI DB
                    .HasMaxLength(16)            // nam/nữ/khác... tuỳ enum của bạn
                    .IsUnicode(false);           // không bắt buộc

                // Media (URL thường dài; để text là an toàn)
                e.Property(u => u.AvatarUrl);
                e.Property(u => u.CoverUrl);

                // Partial unique index (Postgres) để tránh xung đột khi soft-delete
                // -> unique khi IsDeleted = false
                e.HasIndex(u => u.NormalizedUserName)
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false");

                // Email thường non-unique (nếu bạn muốn unique thì cũng filter như trên)
                e.HasIndex(u => u.NormalizedEmail)
                    .HasDatabaseName("ix_users_normalized_email");

                // 1-n navs (bạn có thể bỏ vì Identity đã cấu hình; giữ rõ ràng)
                e.HasMany<UserRole>()
                    .WithOne(ur => ur.User!)
                    .HasForeignKey(ur => ur.UserId)
                    .IsRequired();

                e.HasMany<UserClaim>()
                    .WithOne(uc => uc.User!)
                    .HasForeignKey(uc => uc.UserId)
                    .IsRequired();

                e.HasMany<UserLogin>()
                    .WithOne(ul => ul.User!)
                    .HasForeignKey(ul => ul.UserId)
                    .IsRequired();

                e.HasMany<UserToken>()
                    .WithOne(ut => ut.User!)
                    .HasForeignKey(ut => ut.UserId)
                    .IsRequired();

                // 1-1 NotificationPrefs (mỗi user một record)
                e.HasOne(u => u.NotificationPrefs)
                    .WithOne(p => p.User!)
                    .HasForeignKey<NotificationPrefs>(p => p.UserId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);
                b.Entity<NotificationPrefs>()
                    .HasIndex(p => p.UserId)
                    .IsUnique();
            });

            // ---------- ROLE
            b.Entity<Role>(e =>
            {
                e.Property(r => r.Name).HasMaxLength(256);
                e.Property(r => r.NormalizedName).HasMaxLength(256);

                // unique name khi chưa bị xóa mềm
                e.HasIndex(r => r.NormalizedName)
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false");

                e.HasMany<RoleClaim>()
                    .WithOne(rc => rc.Role!)
                    .HasForeignKey(rc => rc.RoleId)
                    .IsRequired();

                e.HasMany<UserRole>()
                    .WithOne(ur => ur.Role!)
                    .HasForeignKey(ur => ur.RoleId)
                    .IsRequired();
            });

            // ---------- Identity composite keys
            b.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId });
            b.Entity<UserLogin>().HasKey(l => new { l.LoginProvider, l.ProviderKey });
            b.Entity<UserToken>().HasKey(t => new { t.UserId, t.LoginProvider, t.Name });

            // ---------- REFRESH TOKEN
            b.Entity<RefreshToken>(e =>
            {
                e.HasKey(t => t.Id);

                e.Property(t => t.TokenHash)
                    .IsRequired();

                e.HasIndex(t => t.TokenHash)
                    .IsUnique();

                e.HasOne<User>()                 // 1 User - n RefreshTokens
                    .WithMany(u => u.RefreshTokens)
                    .HasForeignKey(t => t.UserId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(t => t.UserId);
            });

            // ---------- GLOBAL QUERY FILTERS (soft-delete)
            b.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
            b.Entity<Role>().HasQueryFilter(r => !r.IsDeleted);

            // Áp filter “ăn theo” để tránh cảnh báo EF Core 10622
            b.Entity<UserClaim>().HasQueryFilter(uc => !uc.User!.IsDeleted);
            b.Entity<UserLogin>().HasQueryFilter(ul => !ul.User!.IsDeleted);
            b.Entity<UserToken>().HasQueryFilter(ut => !ut.User!.IsDeleted);
            b.Entity<RoleClaim>().HasQueryFilter(rc => !rc.Role!.IsDeleted);
            b.Entity<UserRole>().HasQueryFilter(ur => !ur.User!.IsDeleted && !ur.Role!.IsDeleted);
        }

        public override int SaveChanges()
        {
            ApplyAuditStamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            ApplyAuditStamps();
            return base.SaveChangesAsync(ct);
        }

        private void ApplyAuditStamps()
        {
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is IAuditable aud)
                {
                    if (entry.State == EntityState.Added)
                    {
                        if (aud.CreatedAtUtc == default) aud.CreatedAtUtc = now;
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        aud.UpdatedAtUtc = now;
                    }
                }

                // Nếu muốn chuyển hard delete -> soft delete, bật đoạn dưới:
                // if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete sd)
                // {
                //     entry.State = EntityState.Modified;
                //     sd.IsDeleted = true;
                //     sd.DeletedAtUtc = now;
                // }
            }
        }
    }
}
