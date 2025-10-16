using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;

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
            b.Entity<Game>(e =>
            {
                var nameProperty = e.Property(x => x.Name)
                 .IsRequired()
                 .HasMaxLength(128);

                if (Database.IsNpgsql())
                {
                    nameProperty.HasColumnType("character varying(128)");
                }
                else
                {
                    nameProperty.HasColumnType("nvarchar(128)");
                }

                e.HasIndex(x => x.CreatedAtUtc)
                 .HasDatabaseName("IX_Games_CreatedAtUtc");

                if (Database.IsSqlServer())
                {
                    e.HasIndex(x => x.Name)
                     .HasDatabaseName("IX_Games_Name_CI")
                     .IsUnique()
                     .HasFilter("[IsDeleted] = 0");
                }
            });

            b.Entity<UserGame>(e =>
            {
                e.Property(x => x.InGameName)
                 .HasMaxLength(64);

                e.HasOne(x => x.User)
                 .WithMany(u => u.UserGames)
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Game)
                 .WithMany(g => g.Users)
                 .HasForeignKey(x => x.GameId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.GameId)
                 .HasDatabaseName("IX_UserGames_GameId");
                e.HasIndex(x => x.Skill)
                 .HasDatabaseName("IX_UserGames_Skill");
                e.HasIndex(x => x.UserId)
                 .HasDatabaseName("IX_UserGames_UserId");

                var skillConstraint = Database.IsNpgsql()
                    ? "\"Skill\" IS NULL OR \"Skill\" BETWEEN 0 AND 2"
                    : "[Skill] IS NULL OR [Skill] BETWEEN 0 AND 2";

                e.ToTable(tb =>
                    tb.HasCheckConstraint("CK_UserGames_Skill_Range", skillConstraint));
            });

            // ====== Communities ======
            b.Entity<Community>(e =>
            {
                // Phase 9: Discovery & Popularity indexes
                e.HasIndex(c => c.IsPublic).HasDatabaseName("IX_Community_IsPublic");
                e.HasIndex(c => c.School).HasDatabaseName("IX_Community_School");
                e.HasIndex(c => new { c.IsPublic, c.MembersCount }).HasDatabaseName("IX_Community_Public_Members");
                e.HasIndex(c => c.CreatedAtUtc).HasDatabaseName("IX_Community_CreatedAt");
                
                // Keep existing single-column index for MembersCount for other queries
                e.HasIndex(c => c.MembersCount);
            });

            b.Entity<CommunityGame>(e =>
            {
                e.HasOne(x => x.Community).WithMany(x => x.Games)
                 .HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Game).WithMany()
                 .HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Cascade);
                
                // Phase 9: Index for gameId filter in discovery
                e.HasIndex(x => x.CommunityId).HasDatabaseName("IX_CommunityGame_CommunityId");
                e.HasIndex(x => x.GameId).HasDatabaseName("IX_CommunityGame_GameId");
            });

            // ====== Clubs ======
            b.Entity<Club>(e =>
            {
                e.HasOne(c => c.Community).WithMany(x => x.Clubs)
                 .HasForeignKey(c => c.CommunityId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(c => new { c.CommunityId, c.Name }).IsUnique();
                
                // Indexes for club queries
                e.HasIndex(c => c.CommunityId);
                e.HasIndex(c => c.MembersCount);
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
                
                // Check constraint: RequiresPassword => JoinPasswordHash must not be null
                e.HasCheckConstraint("chk_room_password_required", 
                    "(\"JoinPolicy\" <> '2') OR (\"JoinPasswordHash\" IS NOT NULL)");
                
                // Indexes for room queries
                e.HasIndex(r => r.ClubId);
                e.HasIndex(r => r.JoinPolicy);
                e.HasIndex(r => r.MembersCount);
                e.HasIndex(r => r.Capacity);
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
                
                // Indexes for member queries
                e.HasIndex(rm => new { rm.RoomId, rm.Status }); // Composite for filtering by room + status
                e.HasIndex(rm => rm.Status);
                e.HasIndex(rm => rm.UserId);
                
                // Phase 9: Indexes for recent activity tracking (last 48h joins)
                e.HasIndex(rm => rm.JoinedAt).HasDatabaseName("IX_RoomMember_JoinedAt");
                e.HasIndex(rm => new { rm.RoomId, rm.JoinedAt }).HasDatabaseName("IX_RoomMember_Room_JoinedAt");
            });

            // ====== Events ======
            var providerName = Database.ProviderName ?? string.Empty;
            var isNpgsql = providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);

            b.Entity<Event>(e =>
            {
                e.Property(x => x.Mode).HasConversion<string>();
                e.Property(x => x.Status).HasConversion<string>();
                e.Property(x => x.GatewayFeePolicy).HasConversion<string>();
                e.Property(x => x.PlatformFeeRate).HasPrecision(5, 4);

                e.HasIndex(x => x.StartsAt);
                e.HasIndex(x => x.OrganizerId);
                e.HasIndex(x => x.CommunityId);

                e.HasCheckConstraint(
                    "chk_event_price_nonneg",
                    isNpgsql
                        ? "\"PriceCents\" >= 0"
                        : "[PriceCents] >= 0");

                e.HasCheckConstraint(
                    "chk_event_platform_fee_range",
                    isNpgsql
                        ? "\"PlatformFeeRate\" >= 0 AND \"PlatformFeeRate\" <= 1"
                        : "[PlatformFeeRate] >= 0 AND [PlatformFeeRate] <= 1");

                e.HasCheckConstraint(
                    "chk_event_starts_before_ends",
                    isNpgsql
                        ? "\"EndsAt\" IS NULL OR \"StartsAt\" < \"EndsAt\""
                        : "[EndsAt] IS NULL OR [StartsAt] < [EndsAt]");

                var eventStatusConstraint = isNpgsql
                    ? "\"Status\" IN ('Draft','Open','Closed','Completed','Canceled')"
                    : "[Status] IN ('Draft','Open','Closed','Completed','Canceled')";
                e.HasCheckConstraint("chk_event_status_allowed", eventStatusConstraint);

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
                e.HasIndex(x => new { x.EventId, x.Status });

                var registrationStatusConstraint = isNpgsql
                    ? "\"Status\" IN ('Pending','Confirmed','Canceled','Refunded')"
                    : "[Status] IN ('Pending','Confirmed','Canceled','Refunded')";
                e.HasCheckConstraint("chk_event_registration_status_allowed", registrationStatusConstraint);

                e.HasOne(er => er.PaymentIntent)
                 .WithOne(pi => pi.EventRegistration)
                 .HasForeignKey<PaymentIntent>(pi => pi.EventRegistrationId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<Escrow>(e =>
            {
                e.Property(x => x.Status).HasConversion<string>();
                e.HasCheckConstraint(
                    "chk_escrow_amount_nonneg",
                    isNpgsql
                        ? "\"AmountHoldCents\" >= 0"
                        : "[AmountHoldCents] >= 0");

                var escrowStatusConstraint = isNpgsql
                    ? "\"Status\" IN ('Held','Released','Refunded')"
                    : "[Status] IN ('Held','Released','Refunded')";
                e.HasCheckConstraint("chk_escrow_status_allowed", escrowStatusConstraint);

                e.HasIndex(x => x.EventId).IsUnique();
            });

            // ====== Wallet & Payments ======
            b.Entity<Wallet>(e =>
            {
                e.HasOne(x => x.User).WithOne(x => x.Wallet!)
                 .HasForeignKey<Wallet>(x => x.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(x => x.UserId).IsUnique();

                e.HasCheckConstraint(
                    "chk_wallet_balance_nonneg",
                    isNpgsql
                        ? "\"BalanceCents\" >= 0"
                        : "[BalanceCents] >= 0");
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
                e.HasIndex(x => x.WalletId);
                e.HasIndex(x => new { x.WalletId, x.CreatedAtUtc });

                e.HasCheckConstraint(
                    "chk_transaction_amount_positive",
                    isNpgsql
                        ? "\"AmountCents\" > 0"
                        : "[AmountCents] > 0");

                var transactionDirectionConstraint = isNpgsql
                    ? "\"Direction\" IN ('In','Out')"
                    : "[Direction] IN ('In','Out')";
                e.HasCheckConstraint("chk_transaction_direction_allowed", transactionDirectionConstraint);

                var transactionMethodConstraint = isNpgsql
                    ? "\"Method\" IN ('Wallet','Gateway','Manual')"
                    : "[Method] IN ('Wallet','Gateway','Manual')";
                e.HasCheckConstraint("chk_transaction_method_allowed", transactionMethodConstraint);

                var transactionStatusConstraint = isNpgsql
                    ? "\"Status\" IN ('Pending','Succeeded','Failed','Refunded')"
                    : "[Status] IN ('Pending','Succeeded','Failed','Refunded')";
                e.HasCheckConstraint("chk_transaction_status_allowed", transactionStatusConstraint);
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

                e.HasIndex(x => x.UserId);
                e.HasIndex(x => x.EventRegistrationId).IsUnique();

                e.HasCheckConstraint(
                    "chk_payment_intent_amount_positive",
                    isNpgsql
                        ? "\"AmountCents\" > 0"
                        : "[AmountCents] > 0");

                var paymentPurposeConstraint = isNpgsql
                    ? "\"Purpose\" IN ('TopUp','EventTicket')"
                    : "[Purpose] IN ('TopUp','EventTicket')";
                e.HasCheckConstraint("chk_payment_intent_purpose_allowed", paymentPurposeConstraint);

                var paymentStatusConstraint = isNpgsql
                    ? "\"Status\" IN ('RequiresPayment','Succeeded','Canceled','Expired')"
                    : "[Status] IN ('RequiresPayment','Succeeded','Canceled','Expired')";
                e.HasCheckConstraint("chk_payment_intent_status_allowed", paymentStatusConstraint);
            });

            // ====== Gifts ======
            b.Entity<Gift>(e =>
            {
                e.Property(x => x.CostPoints).HasDefaultValue(0);
            });

            // ====== BugReport ======
            b.Entity<BugReport>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Category).HasMaxLength(64).IsRequired();
                e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
                e.Property(x => x.ImageUrl).HasMaxLength(1024);
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
                
                e.HasIndex(x => new { x.UserId, x.CreatedAtUtc }).HasDatabaseName("IX_BugReports_User_CreatedAt");
                e.HasIndex(x => new { x.Status, x.CreatedAtUtc }).HasDatabaseName("IX_BugReports_Status_CreatedAt");

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
