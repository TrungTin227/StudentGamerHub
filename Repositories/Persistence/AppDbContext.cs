using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Persistence
{
    public sealed class AppDbContext : IdentityDbContext<User, Role, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Identity
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        // Domain
        public DbSet<Game> Games => Set<Game>();
        public DbSet<UserGame> UserGames => Set<UserGame>();

        public DbSet<Community> Communities => Set<Community>();
        public DbSet<CommunityGame> CommunityGames => Set<CommunityGame>();

        public DbSet<Club> Clubs => Set<Club>();
        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<RoomMember> RoomMembers => Set<RoomMember>();

        public DbSet<Event> Events => Set<Event>();
        public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();
        public DbSet<Escrow> Escrows => Set<Escrow>();

        public DbSet<Wallet> Wallets => Set<Wallet>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();

        public DbSet<FriendLink> FriendLinks => Set<FriendLink>();
        public DbSet<Gift> Gifts => Set<Gift>();
        public DbSet<BugReport> BugReports => Set<BugReport>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            // ====== Table names (lowercase) ======
            b.Entity<User>().ToTable("users");
            b.Entity<Role>().ToTable("roles");
            b.Entity<RefreshToken>().ToTable("refresh_tokens");

            b.Entity<Game>().ToTable("games");
            b.Entity<UserGame>().ToTable("user_games");

            b.Entity<Community>().ToTable("communities");
            b.Entity<CommunityGame>().ToTable("community_games");

            b.Entity<Club>().ToTable("clubs");
            b.Entity<Room>().ToTable("rooms");
            b.Entity<RoomMember>().ToTable("room_members");

            b.Entity<Event>().ToTable("events");
            b.Entity<EventRegistration>().ToTable("event_registrations");
            b.Entity<Escrow>().ToTable("escrows");

            b.Entity<Wallet>().ToTable("wallets");
            b.Entity<Transaction>().ToTable("transactions");
            b.Entity<PaymentIntent>().ToTable("payment_intents");

            b.Entity<FriendLink>().ToTable("friend_links");
            b.Entity<Gift>().ToTable("gifts");
            b.Entity<BugReport>().ToTable("bug_reports");

            // Identity tables (optional rename)
            b.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
            b.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
            b.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
            b.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
            b.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");

            // ====== USER ======
            b.Entity<User>(e =>
            {
                e.Property(u => u.UserName).HasMaxLength(256);
                e.Property(u => u.NormalizedUserName).HasMaxLength(256);
                e.Property(u => u.Email).HasMaxLength(256);
                e.Property(u => u.NormalizedEmail).HasMaxLength(256);

                e.Property(u => u.FullName).HasMaxLength(256);
                e.Property(u => u.University).HasMaxLength(256);

                e.Property(u => u.Gender).HasConversion<string>().HasMaxLength(16).IsUnicode(false);

                e.HasIndex(u => u.NormalizedUserName)
                 .IsUnique()
                 .HasFilter("\"IsDeleted\" = false");

                e.HasIndex(u => u.NormalizedEmail)
                 .HasDatabaseName("ix_users_normalized_email");

                // Teammates search index
                e.HasIndex(u => u.University);
            });

            // ====== ROLE ======
            b.Entity<Role>(e =>
            {
                e.Property(r => r.Name).HasMaxLength(256);
                e.Property(r => r.NormalizedName).HasMaxLength(256);

                e.HasIndex(r => r.NormalizedName)
                 .IsUnique()
                 .HasFilter("\"IsDeleted\" = false");
            });

            // ====== RefreshToken ======
            b.Entity<RefreshToken>(e =>
            {
                e.HasKey(t => t.Id);
                e.Property(t => t.TokenHash).IsRequired();
                e.HasIndex(t => t.TokenHash).IsUnique();
                e.HasIndex(t => t.UserId);

                e.HasOne(t => t.User)
                 .WithMany(u => u.RefreshTokens)
                 .HasForeignKey(t => t.UserId)
                 .IsRequired()
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ====== FriendLink ======
            b.Entity<FriendLink>(e =>
            {
                e.Property(x => x.Status).HasConversion<string>();

                e.HasOne(x => x.Sender).WithMany()
                 .HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Recipient).WithMany()
                 .HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Cascade);

                e.Property(x => x.PairMinUserId)
                 .HasComputedColumnSql("LEAST(\"SenderId\",\"RecipientId\")", stored: true);
                e.Property(x => x.PairMaxUserId)
                 .HasComputedColumnSql("GREATEST(\"SenderId\",\"RecipientId\")", stored: true);

                e.HasIndex(x => new { x.PairMinUserId, x.PairMaxUserId }).IsUnique();
                e.HasIndex(x => x.SenderId);
                e.HasIndex(x => x.RecipientId);
            });

            // ====== Games & UserGames (N–N) ======
            b.Entity<UserGame>(e =>
            {
                e.Property(x => x.Skill).HasConversion<string>();
                e.Property(x => x.InGameName).HasMaxLength(64);

                e.HasOne(x => x.User)
                 .WithMany(u => u.UserGames)
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Game)
                 .WithMany(g => g.Users)
                 .HasForeignKey(x => x.GameId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(x => x.GameId);
                // Teammates search indexes
                e.HasIndex(x => x.Skill);
            });

            // ====== Communities ======
            b.Entity<Community>(e =>
            {
                // (các field mặc định)
            });

            b.Entity<CommunityGame>(e =>
            {
                e.HasOne(x => x.Community).WithMany(x => x.Games)
                 .HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Game).WithMany()
                 .HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Cascade);
            });

            // ====== Clubs ======
            b.Entity<Club>(e =>
            {
                e.HasOne(c => c.Community).WithMany(x => x.Clubs)
                 .HasForeignKey(c => c.CommunityId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(c => new { c.CommunityId, c.Name }).IsUnique();
            });

            // ====== Rooms ======
            b.Entity<Room>(e =>
            {
                e.Property(x => x.JoinPolicy).HasConversion<string>();
                e.Property(x => x.JoinPasswordHash).HasMaxLength(256);

                e.HasOne(r => r.Club).WithMany(c => c.Rooms)
                 .HasForeignKey(r => r.ClubId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(r => new { r.ClubId, r.Name }).IsUnique();
                e.HasCheckConstraint("chk_room_capacity_nonneg", "\"Capacity\" IS NULL OR \"Capacity\" >= 0");
            });

            // ====== RoomMembers ======
            b.Entity<RoomMember>(e =>
            {
                e.Property(x => x.Role).HasConversion<string>();
                e.Property(x => x.Status).HasConversion<string>();

                e.HasOne(rm => rm.Room).WithMany(r => r.Members)
                 .HasForeignKey(rm => rm.RoomId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(rm => rm.User).WithMany()
                 .HasForeignKey(rm => rm.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                // PK đã đặt qua [PrimaryKey(RoomId, UserId)]
                e.HasIndex(rm => new { rm.RoomId, rm.UserId }).IsUnique();
            });

            // ====== Events ======
            b.Entity<Event>(e =>
            {
                e.Property(x => x.Mode).HasConversion<string>();
                e.Property(x => x.Status).HasConversion<string>();
                e.Property(x => x.GatewayFeePolicy).HasConversion<string>();
                e.Property(x => x.PlatformFeeRate).HasPrecision(5, 4);

                e.HasOne(x => x.Community).WithMany()
                 .HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Organizer).WithMany()
                 .HasForeignKey(x => x.OrganizerId).OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Escrow).WithOne(x => x.Event!)
                 .HasForeignKey<Escrow>(x => x.EventId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<EventRegistration>(e =>
            {
                e.Property(x => x.Status).HasConversion<string>();

                e.HasOne(x => x.Event).WithMany(x => x.Registrations)
                 .HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.User).WithMany()
                 .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.PaidTransaction).WithOne()
                 .HasForeignKey<EventRegistration>(x => x.PaidTransactionId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasIndex(x => x.PaidTransactionId).IsUnique();

                e.HasIndex(x => new { x.EventId, x.UserId }).IsUnique();

                e.HasOne(er => er.PaymentIntent)
                 .WithOne(pi => pi.EventRegistration)
                 .HasForeignKey<PaymentIntent>(pi => pi.EventRegistrationId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<Escrow>(e =>
            {
                e.Property(x => x.Status).HasConversion<string>();
                e.HasCheckConstraint("chk_escrow_amount_nonneg", "\"AmountHoldCents\" >= 0");
            });

            // ====== Wallet & Payments ======
            b.Entity<Wallet>(e =>
            {
                e.HasOne(x => x.User).WithOne(x => x.Wallet!)
                 .HasForeignKey<Wallet>(x => x.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(x => x.UserId).IsUnique();
            });

            b.Entity<Transaction>(e =>
            {
                e.Property(x => x.Direction).HasConversion<string>();
                e.Property(x => x.Method).HasConversion<string>();
                e.Property(x => x.Status).HasConversion<string>();

                e.HasOne(x => x.Wallet).WithMany(x => x.Transactions)
                 .HasForeignKey(x => x.WalletId)
                 .IsRequired()
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Event).WithMany()
                 .HasForeignKey(x => x.EventId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasIndex(x => x.EventId);
                e.HasIndex(x => new { x.WalletId, x.CreatedAtUtc });
            });

            b.Entity<PaymentIntent>(e =>
            {
                e.Property(x => x.Purpose).HasConversion<string>();
                e.Property(x => x.Status).HasConversion<string>();
                e.Property(x => x.ClientSecret).IsRequired();

                e.HasOne(x => x.User)
                 .WithMany()
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.EventRegistrationId).IsUnique();

                e.HasCheckConstraint(
                    "CK_PI_EventTicket_RequiresER",
                    "\"Purpose\" <> 'EventTicket' OR \"EventRegistrationId\" IS NOT NULL");

                e.HasCheckConstraint(
                    "CK_PI_NonTicket_NoER",
                    "\"Purpose\" = 'EventTicket' OR \"EventRegistrationId\" IS NULL");
            });

            // ====== Gifts ======
            b.Entity<Gift>(e =>
            {
                e.Property(x => x.CostPoints).HasDefaultValue(0);
            });

            // ====== BugReport ======
            b.Entity<BugReport>(e =>
            {
                e.Property(x => x.Status).HasConversion<string>();

                e.HasOne(x => x.User).WithMany()
                 .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            });

            // ====== Global Query Filters (soft-delete matching) ======
            b.Entity<Community>().HasQueryFilter(c => !c.IsDeleted);
            b.Entity<CommunityGame>().HasQueryFilter(cg => !cg.Community!.IsDeleted);

            b.Entity<Club>().HasQueryFilter(cl => !cl.IsDeleted && !cl.Community!.IsDeleted);
            b.Entity<Room>().HasQueryFilter(r => !r.IsDeleted && !r.Club!.IsDeleted && !r.Club!.Community!.IsDeleted);
            b.Entity<RoomMember>().HasQueryFilter(rm => !rm.User!.IsDeleted && !rm.Room!.IsDeleted && !rm.Room!.Club!.IsDeleted && !rm.Room!.Club!.Community!.IsDeleted);

            // (Tuỳ chọn) filter cho Game/UserGame nếu bạn có soft-delete ở Game
            // b.Entity<Game>().HasQueryFilter(g => !g.IsDeleted);
            // b.Entity<UserGame>().HasQueryFilter(ug => !ug.User!.IsDeleted && !ug.Game!.IsDeleted);

            // Helper: tự áp cho ISoftDelete còn lại (nếu bạn đã viết extension này)
            b.ApplySoftDeleteFilters();
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

                if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete sd)
                {
                    entry.State = EntityState.Modified;
                    sd.IsDeleted = true;
                    sd.DeletedAtUtc = now;
                }
            }
        }
    }
}
