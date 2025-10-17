using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Repositories.Persistence.Seeding;

/// <summary>
/// Seeds comprehensive test data for all entities in the application for development/demo environments.
/// Idempotent: chạy nhiều lần không tạo trùng/vi phạm unique constraints.
/// </summary>
public sealed class AppSeeder : IAppSeeder
{
    private static readonly string[] DefaultGames =
    [
        "Valorant",
        "League of Legends",
        "Dota 2",
        "Apex Legends",
        "Counter-Strike 2",
        "Overwatch 2",
        "PUBG: Battlegrounds",
        "Fortnite",
        "Rocket League",
        "Genshin Impact"
    ];

    private static readonly string[] Universities =
    [
        "Đại học Bách khoa Hà Nội",
        "Đại học Quốc gia Hà Nội",
        "Đại học FPT",
        "Đại học Công nghệ - ĐHQGHN",
        "Đại học Kinh tế Quốc dân",
        "Đại học Thương mại",
        "Đại học Ngoại thương",
        "Đại học Sư phạm Hà Nội",
        "Học viện Ngân hàng",
        "Đại học Thuỷ lợi"
    ];

    private static readonly string[] CommunityNames =
    [
        "Valorant VN Community",
        "LoL Vietnam",
        "Dota 2 Hanoi",
        "FPT Gaming Hub",
        "Apex Legends VN",
        "CS2 Vietnam",
        "Overwatch Vietnam",
        "PUBG University League",
        "Rocket League VN",
        "Genshin Impact Vietnam"
    ];

    private static readonly string[] ClubNames =
    [
        "Competitive Team",
        "Casual Players",
        "New Players Welcome",
        "Pro Training",
        "Weekend Warriors",
        "Study Break Gaming",
        "Tournament Prep",
        "Beginner Friendly",
        "Advanced Strategies",
        "Fun & Games"
    ];

    private static readonly string[] RoomNames =
    [
        "General Chat",
        "LFG - Looking for Group",
        "Strategy Discussion",
        "Tournament Talk",
        "Newbie Help",
        "Off-topic",
        "Announcements",
        "Voice Chat Room",
        "Coaching Corner",
        "Memes & Fun"
    ];

    private static readonly string[] EventTitles =
    [
        "Weekly Tournament",
        "Newbie Championship",
        "Pro League Qualifier",
        "Community Cup",
        "Spring Championship",
        "University League",
        "Practice Match",
        "Skill Assessment",
        "Fun Tournament",
        "Monthly Challenge"
    ];

    private static readonly string[] GiftNames =
    [
        "Gaming Mouse Pad",
        "Mechanical Keyboard",
        "Gaming Headset",
        "Monitor Stand",
        "RGB Mouse",
        "Webcam HD",
        "Gaming Chair Cushion",
        "Blue Light Glasses",
        "Desk Organizer",
        "Gaming Mug"
    ];

    private readonly AppDbContext _db;
    private readonly IGameRepository _games;
    private readonly IUserGameRepository _userGames;
    private readonly IGenericUnitOfWork _uow;
    private readonly ILogger<AppSeeder> _logger;
    private readonly UserManager<User> _userManager;
    private readonly SeedOptions _seedOptions;

    public AppSeeder(
        AppDbContext db,
        IGameRepository games,
        IUserGameRepository userGames,
        IGenericUnitOfWork uow,
        ILogger<AppSeeder> logger,
        UserManager<User> userManager,
        IOptions<SeedOptions> seedOptions)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _userGames = userGames ?? throw new ArgumentNullException(nameof(userGames));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _seedOptions = seedOptions?.Value ?? throw new ArgumentNullException(nameof(seedOptions));
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var result = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            _logger.LogInformation("Starting comprehensive database seeding...");

            // 1) Games
            await SeedGamesAsync(innerCt);
            await _uow.SaveChangesAsync(innerCt);

            // 2) Sample Users
            if (_seedOptions.ComprehensiveSeeding.SeedSampleUsers)
            {
                await SeedSampleUsersAsync(innerCt);
                await _uow.SaveChangesAsync(innerCt);

                // 3) UserGames (depends on users + games)
                await SeedUserGamesAsync(innerCt);
                await _uow.SaveChangesAsync(innerCt);
            }

            // 4) Communities
            if (_seedOptions.ComprehensiveSeeding.SeedCommunities)
            {
                await SeedCommunitiesAsync(innerCt);
                await _uow.SaveChangesAsync(innerCt);

                // 5) CommunityGames
                await SeedCommunityGamesAsync(innerCt);
                await _uow.SaveChangesAsync(innerCt);
            }

            // 6) Clubs & Rooms (+ members)
            if (_seedOptions.ComprehensiveSeeding.SeedClubsAndRooms)
            {
                await SeedClubsAsync(innerCt);
                await _uow.SaveChangesAsync(innerCt);

                await SeedRoomsAsync(innerCt);
                await _uow.SaveChangesAsync(innerCt);

                await SeedRoomMembersAsync(innerCt);
                await _uow.SaveChangesAsync(innerCt);
            }

            // 7) Events (+ registrations)
            if (_seedOptions.ComprehensiveSeeding.SeedEvents)
            {
                await SeedEventsAsync(innerCt);
                await _uow.SaveChangesAsync(innerCt);

                await SeedEventRegistrationsAsync(innerCt);
                await _uow.SaveChangesAsync(innerCt);
            }

            // 8) Wallets & Transactions
            if (_seedOptions.ComprehensiveSeeding.SeedWalletsAndTransactions)
            {
                await SeedWalletsAsync(innerCt);
                await _uow.SaveChangesAsync(innerCt);

                await SeedTransactionsAsync(innerCt);
                await _uow.SaveChangesAsync(innerCt);
            }

            // 9) Friend Links
            if (_seedOptions.ComprehensiveSeeding.SeedFriendships)
            {
                await SeedFriendLinksAsync(innerCt);
                await _uow.SaveChangesAsync(innerCt);
            }

            // 10) Gifts
            if (_seedOptions.ComprehensiveSeeding.SeedGifts)
            {
                await SeedGiftsAsync(innerCt);
                await _uow.SaveChangesAsync(innerCt);
            }

            // 11) Bug Reports
            if (_seedOptions.ComprehensiveSeeding.SeedBugReports)
            {
                await SeedBugReportsAsync(innerCt);
                await _uow.SaveChangesAsync(innerCt);
            }

            _logger.LogInformation("✔ Comprehensive database seeding completed successfully!");
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        if (result.IsFailure)
            _logger.LogWarning("Comprehensive app seeding failed: {Message}", result.Error.Message);
    }

    // -------------------
    // Games (idempotent)
    // -------------------
    private async Task SeedGamesAsync(CancellationToken ct)
    {
        var existingNames = await _games.Query()
            .Select(g => g.Name)
            .ToListAsync(ct);

        var comparer = StringComparer.OrdinalIgnoreCase;
        var newGames = DefaultGames
            .Where(name => !existingNames.Contains(name, comparer))
            .Select(name => new Game
            {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-Random.Shared.Next(30, 365))
            })
            .ToList();

        foreach (var game in newGames)
            await _games.CreateAsync(game, ct);

        if (newGames.Count > 0)
            _logger.LogInformation("Seeded {Count} games.", newGames.Count);
    }

    // -----------------------------------------
    // Sample Users (idempotent, deterministic)
    // -----------------------------------------
    private async Task SeedSampleUsersAsync(CancellationToken ct)
    {
        var maxUsers = _seedOptions.ComprehensiveSeeding.MaxSampleUsers;

        // Lấy các email sample đang tồn tại theo mẫu userNN@example.com
        var sampleEmails = await _db.Users.AsNoTracking()
            .Where(u => u.Email != null &&
                        u.Email.EndsWith("@example.com") &&
                        u.Email.StartsWith("user"))
            .Select(u => u.Email!)
            .ToListAsync(ct);

        var existingIndexes = new HashSet<int>();
        foreach (var email in sampleEmails)
        {
            var at = email.IndexOf('@');
            if (at > 4 && int.TryParse(email.Substring(4, at - 4), out var idx))
                existingIndexes.Add(idx);
        }

        var rnd = Random.Shared;
        var created = 0;

        for (int idx = 1; idx <= maxUsers; idx++)
        {
            if (existingIndexes.Contains(idx)) continue;

            var email = $"user{idx:00}@example.com";
            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = $"Test User {idx:00}",
                University = Universities[rnd.Next(Universities.Length)],
                Gender = (Gender)rnd.Next(0, 3),
                Level = rnd.Next(1, 50),
                Points = rnd.Next(0, 10000),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-rnd.Next(1, 180))
            };

            var result = await _userManager.CreateAsync(user, "User@123");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");
                created++;
            }
            else
            {
                _logger.LogWarning("Skip creating sample user {Email}: {Errors}",
                    email, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        if (created > 0)
            _logger.LogInformation("Seeded {Count} sample users.", created);
    }

    // -----------------------------------
    // UserGames (idempotent per (U,G))
    // -----------------------------------
    private async Task SeedUserGamesAsync(CancellationToken ct)
    {
        var games = await _games.Query()
            .Select(g => new { g.Id, g.Name })
            .ToListAsync(ct);
        if (games.Count == 0) return;

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.CreatedAtUtc)
            .Select(u => new { u.Id })
            .ToListAsync(ct);
        if (users.Count == 0) return;

        var rnd = Random.Shared;

        // Build existing pairs to avoid duplicates
        var existingPairs = await _userGames.Query()
            .Select(ug => new { ug.UserId, ug.GameId })
            .ToListAsync(ct);

        var existing = existingPairs
            .Select(p => (p.UserId, p.GameId))
            .ToHashSet();

        foreach (var u in users)
        {
            // chọn 1-4 game ngẫu nhiên chưa có
            var available = games
                .Where(g => !existing.Contains((u.Id, g.Id)))
                .OrderBy(_ => rnd.Next())
                .ToList();

            if (available.Count == 0) continue;

            var count = rnd.Next(1, Math.Min(4, available.Count) + 1);
            foreach (var game in available.Take(count))
            {
                var skillIndex = rnd.Next(0, Enum.GetValues<GameSkillLevel>().Length);
                var inGameName = $"{game.Name.Replace(' ', '_')}#{rnd.Next(1000, 9999)}";

                var userGame = new UserGame
                {
                    UserId = u.Id,
                    GameId = game.Id,
                    InGameName = inGameName,
                    Skill = (GameSkillLevel)skillIndex,
                    AddedAt = DateTime.UtcNow.AddDays(-rnd.Next(0, 90)),
                    CreatedBy = u.Id,
                    UpdatedBy = u.Id,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-rnd.Next(0, 90))
                };

                await _userGames.CreateAsync(userGame, ct);
                existing.Add((u.Id, game.Id));
            }
        }

        _logger.LogInformation("Seeded UserGame relationships.");
    }

    // ---------------------------------
    // Communities (idempotent by Name)
    // ---------------------------------
    private async Task SeedCommunitiesAsync(CancellationToken ct)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var existingNames = await _db.Communities
            .AsNoTracking()
            .Select(c => c.Name)
            .ToListAsync(ct);

        var rnd = Random.Shared;
        var toAdd = CommunityNames
            .Where(n => !existingNames.Contains(n, comparer))
            .Take(_seedOptions.ComprehensiveSeeding.MaxCommunities)
            .ToList();

        foreach (var name in toAdd)
        {
            var community = new Community
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = $"This is a community for {name} players. Join us for tournaments, discussions, and fun!",
                School = Universities[rnd.Next(Universities.Length)],
                IsPublic = rnd.NextDouble() > 0.2,
                MembersCount = rnd.Next(10, 500),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-rnd.Next(1, 200))
            };

            _db.Communities.Add(community);
        }

        if (toAdd.Count > 0)
            _logger.LogInformation("Seeded {Count} communities.", toAdd.Count);
    }

    // ----------------------------------------------------
    // CommunityGames (idempotent per (Community, Game))
    // ----------------------------------------------------
    private async Task SeedCommunityGamesAsync(CancellationToken ct)
    {
        var communities = await _db.Communities
            .AsNoTracking()
            .Select(c => c.Id)
            .ToListAsync(ct);
        var games = await _db.Games
            .AsNoTracking()
            .Select(g => g.Id)
            .ToListAsync(ct);

        if (communities.Count == 0 || games.Count == 0) return;

        var rnd = Random.Shared;

        var existingPairs = await _db.CommunityGames
            .AsNoTracking()
            .Select(cg => new { cg.CommunityId, cg.GameId })
            .ToListAsync(ct);

        var existing = existingPairs
            .Select(p => (p.CommunityId, p.GameId))
            .ToHashSet();

        var created = 0;

        foreach (var communityId in communities)
        {
            var candidates = games
                .Where(gid => !existing.Contains((communityId, gid)))
                .OrderBy(_ => rnd.Next())
                .Take(rnd.Next(1, 4)) // 1-3 games/community
                .ToList();

            foreach (var gameId in candidates)
            {
                _db.CommunityGames.Add(new CommunityGame
                {
                    CommunityId = communityId,
                    GameId = gameId
                });
                existing.Add((communityId, gameId));
                created++;
            }
        }

        if (created > 0)
            _logger.LogInformation("Seeded {Count} CommunityGame links.", created);
    }

    // ------------------------------------------
    // Clubs (idempotent per (CommunityId, Name))
    // ------------------------------------------
    private async Task SeedClubsAsync(CancellationToken ct)
    {
        var communities = await _db.Communities
            .AsNoTracking()
            .Select(c => new { c.Id, c.Name, c.CreatedAtUtc })
            .ToListAsync(ct);
        if (communities.Count == 0) return;

        var comparer = StringComparer.OrdinalIgnoreCase;

        var existingByCommunity = await _db.Clubs
            .AsNoTracking()
            .GroupBy(c => c.CommunityId)
            .ToDictionaryAsync(
                g => g.Key,
                g => new HashSet<string>(g.Select(x => x.Name), comparer),
                ct);

        var rnd = Random.Shared;
        var created = 0;

        foreach (var community in communities)
        {
            if (!existingByCommunity.TryGetValue(community.Id, out var used))
                used = existingByCommunity[community.Id] = new HashSet<string>(comparer);

            var target = rnd.Next(1, 4); // 1–3 clubs/community
            var names = ClubNames
                .Where(n => !used.Contains(n))
                .OrderBy(_ => rnd.Next())
                .Take(target)
                .ToList();

            foreach (var name in names)
            {
                _db.Clubs.Add(new Club
                {
                    Id = Guid.NewGuid(),
                    CommunityId = community.Id,
                    Name = name,
                    Description = $"A club for {name.ToLower()} in {community.Name}",
                    IsPublic = rnd.NextDouble() > 0.3,
                    MembersCount = rnd.Next(5, 100),
                    CreatedAtUtc = community.CreatedAtUtc.AddDays(rnd.Next(1, 30))
                });

                used.Add(name);
                created++;
            }
        }

        if (created > 0)
            _logger.LogInformation("Seeded {Count} clubs (idempotent).", created);
    }

    // ---------------------------------------
    // Rooms (idempotent per (ClubId, Name))
    // ---------------------------------------
    private async Task SeedRoomsAsync(CancellationToken ct)
    {
        var clubs = await _db.Clubs
            .AsNoTracking()
            .Select(c => new { c.Id, c.CreatedAtUtc })
            .ToListAsync(ct);
        if (clubs.Count == 0) return;

        var comparer = StringComparer.OrdinalIgnoreCase;

        var existingByClub = await _db.Rooms
            .AsNoTracking()
            .GroupBy(r => r.ClubId)
            .ToDictionaryAsync(
                g => g.Key,
                g => new HashSet<string>(g.Select(x => x.Name), comparer),
                ct);

        var rnd = Random.Shared;
        var created = 0;

        foreach (var club in clubs)
        {
            if (!existingByClub.TryGetValue(club.Id, out var used))
                used = existingByClub[club.Id] = new HashSet<string>(comparer);

            var target = rnd.Next(2, 6); // 2–5 rooms/club
            var names = RoomNames
                .Where(n => !used.Contains(n))
                .OrderBy(_ => rnd.Next())
                .Take(target)
                .ToList();

            foreach (var name in names)
            {
                var joinPolicy = (RoomJoinPolicy)rnd.Next(0, 3);

                _db.Rooms.Add(new Room
                {
                    Id = Guid.NewGuid(),
                    ClubId = club.Id,
                    Name = name,
                    Description = $"Room for {name.ToLower()}",
                    JoinPolicy = joinPolicy,
                    JoinPasswordHash = joinPolicy == RoomJoinPolicy.RequiresPassword ? BCrypt.Net.BCrypt.HashPassword("password123") : null,
                    Capacity = rnd.NextDouble() > 0.5 ? rnd.Next(10, 100) : (int?)null,
                    MembersCount = rnd.Next(1, 50),
                    CreatedAtUtc = club.CreatedAtUtc.AddDays(rnd.Next(1, 20))
                });

                used.Add(name);
                created++;
            }
        }

        if (created > 0)
            _logger.LogInformation("Seeded {Count} rooms (idempotent).", created);
    }

    // ------------------------------------------------------------------
    // RoomMembers (idempotent per (RoomId, UserId); đảm bảo có 1 Owner)
    // ------------------------------------------------------------------
    private async Task SeedRoomMembersAsync(CancellationToken ct)
    {
        var rooms = await _db.Rooms
            .AsNoTracking()
            .Select(r => new { r.Id, r.CreatedAtUtc })
            .ToListAsync(ct);

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.CreatedAtUtc)
            .Take(50) // lấy rộng hơn chút
            .Select(u => new { u.Id, u.CreatedAtUtc })
            .ToListAsync(ct);

        if (rooms.Count == 0 || users.Count == 0) return;

        var rnd = Random.Shared;

        var existingPairs = await _db.RoomMembers
            .AsNoTracking()
            .Select(rm => new { rm.RoomId, rm.UserId, rm.Role })
            .ToListAsync(ct);

        var pairSet = existingPairs
            .Select(p => (p.RoomId, p.UserId))
            .ToHashSet();

        var owners = existingPairs
            .Where(p => p.Role == RoomRole.Owner)
            .Select(p => p.RoomId)
            .ToHashSet();

        var created = 0;

        foreach (var room in rooms)
        {
            // target members cho mỗi room
            var target = rnd.Next(5, Math.Min(15, users.Count));

            // đã có bao nhiêu thành viên?
            var currentCount = existingPairs.Count(p => p.RoomId == room.Id);
            var need = Math.Max(0, target - currentCount);
            if (need == 0) continue;

            var candidates = users
                .OrderBy(_ => rnd.Next())
                .Where(u => !pairSet.Contains((room.Id, u.Id)))
                .Take(need)
                .ToList();

            var hasOwner = owners.Contains(room.Id);

            foreach (var user in candidates)
            {
                var role = RoomRole.Member;

                if (!hasOwner)
                {
                    role = RoomRole.Owner;
                    owners.Add(room.Id);
                    hasOwner = true;
                }
                else
                {
                    role = rnd.NextDouble() > 0.8 ? RoomRole.Moderator : RoomRole.Member;
                }

                var rm = new RoomMember
                {
                    RoomId = room.Id,
                    UserId = user.Id,
                    Role = role,
                    Status = rnd.NextDouble() > 0.1 ? RoomMemberStatus.Approved : RoomMemberStatus.Pending,
                    JoinedAt = room.CreatedAtUtc.AddDays(rnd.Next(0, 10)),
                    CreatedAtUtc = room.CreatedAtUtc.AddDays(rnd.Next(0, 10))
                };

                _db.RoomMembers.Add(rm);
                pairSet.Add((room.Id, user.Id));
                created++;
            }
        }

        if (created > 0)
            _logger.LogInformation("Seeded {Count} room members (idempotent).", created);
    }

    // ---------------------------------------------------------
    // Events (idempotent: top-up đến MaxEvents tổng trong DB)
    // ---------------------------------------------------------
    private async Task SeedEventsAsync(CancellationToken ct)
    {
        var communities = await _db.Communities
            .AsNoTracking()
            .Take(5)
            .Select(c => new { c.Id, c.Name, c.School })
            .ToListAsync(ct);

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.CreatedAtUtc)
            .Take(20)
            .Select(u => new { u.Id })
            .ToListAsync(ct);

        if (communities.Count == 0 || users.Count == 0) return;

        var maxEvents = _seedOptions.ComprehensiveSeeding.MaxEvents;
        var existingCount = await _db.Events.CountAsync(ct);
        var toCreate = Math.Max(0, maxEvents - existingCount);
        if (toCreate == 0) return;

        var rnd = Random.Shared;
        var created = 0;

        for (int i = 0; i < toCreate; i++)
        {
            var community = communities[rnd.Next(communities.Count)];
            var organizer = users[rnd.Next(users.Count)];
            var startsAt = DateTime.UtcNow.AddDays(rnd.Next(-30, 60));

            var title = $"{EventTitles[rnd.Next(EventTitles.Length)]} - {community.Name}";

            var ev = new Event
            {
                Id = Guid.NewGuid(),
                CommunityId = community.Id,
                OrganizerId = organizer.Id,
                Title = title,
                Description = "Join us for an exciting gaming event! Great prizes and fun competition await.",
                Mode = (EventMode)rnd.Next(0, 2),
                Location = rnd.NextDouble() > 0.5 ? "Online Discord Server" : $"Room {rnd.Next(100, 999)}, {community.School}",
                StartsAt = startsAt,
                EndsAt = startsAt.AddHours(rnd.Next(2, 8)),
                PriceCents = rnd.Next(0, 2) == 0 ? 0 : rnd.Next(50_000, 500_000),
                Capacity = rnd.Next(10, 100),
                EscrowMinCents = 0,
                PlatformFeeRate = 0.07m,
                GatewayFeePolicy = (GatewayFeePolicy)rnd.Next(0, 2),
                Status = (EventStatus)rnd.Next(0, 5),
                CreatedAtUtc = startsAt.AddDays(-rnd.Next(1, 30))
            };

            _db.Events.Add(ev);

            if (ev.PriceCents > 0)
            {
                _db.Escrows.Add(new Escrow
                {
                    Id = Guid.NewGuid(),
                    EventId = ev.Id,
                    AmountHoldCents = ev.PriceCents * rnd.Next(5, 20),
                    Status = (EscrowStatus)rnd.Next(0, 3),
                    CreatedAtUtc = ev.CreatedAtUtc
                });
            }

            created++;
        }

        if (created > 0)
            _logger.LogInformation("Seeded {Count} events (idempotent to MaxEvents).", created);
    }

    // -------------------------------------------------------------------
    // EventRegistrations (idempotent per (EventId, UserId); top-up mỗi event)
    // -------------------------------------------------------------------
    private async Task SeedEventRegistrationsAsync(CancellationToken ct)
    {
        var events = await _db.Events
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(10)
            .Select(e => new { e.Id, e.StartsAt, e.CreatedAtUtc })
            .ToListAsync(ct);

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.CreatedAtUtc)
            .Take(50)
            .Select(u => new { u.Id })
            .ToListAsync(ct);

        if (events.Count == 0 || users.Count == 0) return;

        var rnd = Random.Shared;

        var existingPairs = await _db.EventRegistrations
            .AsNoTracking()
            .Select(r => new { r.EventId, r.UserId })
            .ToListAsync(ct);

        var regSet = existingPairs
            .Select(p => (p.EventId, p.UserId))
            .ToHashSet();

        var created = 0;

        foreach (var ev in events)
        {
            var desired = rnd.Next(8, Math.Min(15, users.Count)); // target participants cho event
            var current = existingPairs.Count(p => p.EventId == ev.Id);
            var need = Math.Max(0, desired - current);
            if (need == 0) continue;

            var candidates = users
                .OrderBy(_ => rnd.Next())
                .Where(u => !regSet.Contains((ev.Id, u.Id)))
                .Take(need)
                .ToList();

            foreach (var user in candidates)
            {
                var registration = new EventRegistration
                {
                    EventId = ev.Id,
                    UserId = user.Id,
                    Status = (EventRegistrationStatus)rnd.Next(0, 5),
                    RegisteredAt = ev.CreatedAtUtc.AddDays(rnd.Next(1, 10)),
                    CheckInAt = rnd.NextDouble() > 0.3 ? ev.StartsAt.AddMinutes(rnd.Next(-30, 60)) : null,
                    CreatedAtUtc = ev.CreatedAtUtc.AddDays(rnd.Next(1, 10))
                };

                _db.EventRegistrations.Add(registration);
                regSet.Add((ev.Id, user.Id));
                created++;
            }
        }

        if (created > 0)
            _logger.LogInformation("Seeded {Count} event registrations (idempotent).", created);
    }

    // ---------------------------------------
    // Wallets (idempotent: only if null)
    // ---------------------------------------
    private async Task SeedWalletsAsync(CancellationToken ct)
    {
        var users = await _db.Users
            .Where(u => !u.IsDeleted && u.Wallet == null)
            .Take(50)
            .Select(u => new { u.Id, u.CreatedAtUtc })
            .ToListAsync(ct);

        if (users.Count == 0) return;

        var rnd = Random.Shared;

        foreach (var u in users)
        {
            var wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                UserId = u.Id,
                BalanceCents = rnd.Next(0, 1_000_000),
                CreatedAtUtc = u.CreatedAtUtc.AddDays(1)
            };

            _db.Wallets.Add(wallet);
        }

        _logger.LogInformation("Seeded {Count} wallets.", users.Count);
    }

    // -------------------------------------------------------------------
    // Transactions (idempotent theo ngưỡng/định mức per wallet, không bơm vô hạn)
    // -------------------------------------------------------------------
    private async Task SeedTransactionsAsync(CancellationToken ct)
    {
        var wallets = await _db.Wallets
            .AsNoTracking()
            .Take(30)
            .Select(w => new { w.Id, w.CreatedAtUtc })
            .ToListAsync(ct);
        if (wallets.Count == 0) return;

        var rnd = Random.Shared;

        // lấy current count per wallet
        var currentPerWallet = await _db.Transactions
            .AsNoTracking()
            .GroupBy(t => t.WalletId)
            .Select(g => new { WalletId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WalletId, x => x.Count, ct);

        var created = 0;

        foreach (var w in wallets)
        {
            var current = currentPerWallet.TryGetValue(w.Id, out var cnt) ? cnt : 0;
            // Target 3–7 transactions/wallet
            var target = rnd.Next(3, 8);
            var need = Math.Max(0, target - current);
            if (need == 0) continue;

            for (int i = 0; i < need; i++)
            {
                var direction = (TransactionDirection)rnd.Next(0, 2);

                _db.Transactions.Add(new Transaction
                {
                    Id = Guid.NewGuid(),
                    WalletId = w.Id,
                    AmountCents = rnd.Next(10_000, 500_000),
                    Currency = "VND",
                    Direction = direction,
                    Method = (TransactionMethod)rnd.Next(0, 3),
                    Status = (TransactionStatus)rnd.Next(0, 5),
                    Provider = rnd.NextDouble() > 0.5 ? "VNPay" : "MoMo",
                    ProviderRef = $"TXN{rnd.Next(100000, 999999)}",
                    CreatedAtUtc = w.CreatedAtUtc.AddDays(rnd.Next(1, 30))
                });
                created++;
            }
        }

        if (created > 0)
            _logger.LogInformation("Seeded {Count} transactions (idempotent up to target per wallet).", created);
    }

    // ----------------------------------------------------------------------
    // FriendLinks (idempotent theo cặp unordered; top-up đến MaxFriendLinks)
    // ----------------------------------------------------------------------
    private async Task SeedFriendLinksAsync(CancellationToken ct)
    {
        var users = await _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.CreatedAtUtc)
            .Take(50)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (users.Count < 2) return;

        var rnd = Random.Shared;

        // cặp đã tồn tại (min, max)
        var existingLinks = await _db.FriendLinks
            .AsNoTracking()
            .Select(fl => new { fl.SenderId, fl.RecipientId })
            .ToListAsync(ct);

        static (Guid min, Guid max) Pair(Guid a, Guid b) =>
            a.CompareTo(b) < 0 ? (a, b) : (b, a);

        var linkSet = existingLinks
            .Select(x => Pair(x.SenderId, x.RecipientId))
            .ToHashSet();

        var existingCount = linkSet.Count;
        var toCreate = Math.Max(0, _seedOptions.ComprehensiveSeeding.MaxFriendLinks - existingCount);
        if (toCreate == 0) return;

        var created = 0;

        // tạo tối đa 'toCreate' link mới
        for (int i = 0; i < toCreate * 2 && created < toCreate; i++) // *2 để tăng cơ hội tìm được cặp mới
        {
            var sender = users[rnd.Next(users.Count)];
            var recipient = users[rnd.Next(users.Count)];
            if (sender == recipient) continue;

            var pair = Pair(sender, recipient);
            if (linkSet.Contains(pair)) continue;

            var status = (FriendStatus)rnd.Next(0, 3);
            var link = new FriendLink
            {
                Id = Guid.NewGuid(),
                SenderId = pair.min,
                RecipientId = pair.max,
                Status = status,
                RespondedAt = status != FriendStatus.Pending ? DateTime.UtcNow.AddDays(-rnd.Next(1, 30)) : null,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-rnd.Next(1, 60))
            };

            _db.FriendLinks.Add(link);
            linkSet.Add(pair);
            created++;
        }

        if (created > 0)
            _logger.LogInformation("Seeded {Count} friend links (idempotent up to MaxFriendLinks).", created);
    }

    // --------------------------------------------
    // Gifts (idempotent by Name, top-up đến 10)
    // --------------------------------------------
    private async Task SeedGiftsAsync(CancellationToken ct)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var existingNames = await _db.Gifts
            .AsNoTracking()
            .Select(g => g.Name)
            .ToListAsync(ct);

        var rnd = Random.Shared;

        var toAdd = GiftNames
            .Where(n => !existingNames.Contains(n, comparer))
            .Take(Math.Max(0, 10 - existingNames.Count))
            .ToList();

        foreach (var name in toAdd)
        {
            _db.Gifts.Add(new Gift
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = $"High-quality {name.ToLower()} for gamers. Limited time offer!",
                CostPoints = rnd.Next(100, 2000),
                StockQty = rnd.Next(10, 100),
                IsActive = rnd.NextDouble() > 0.1,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-rnd.Next(1, 100))
            });
        }

        if (toAdd.Count > 0)
            _logger.LogInformation("Seeded {Count} gifts (idempotent).", toAdd.Count);
    }

    // ------------------------------------------------------
    // BugReports (idempotent: top-up đến MaxBugReports)
    // ------------------------------------------------------
    private async Task SeedBugReportsAsync(CancellationToken ct)
    {
        var users = await _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.CreatedAtUtc)
            .Take(20)
            .Select(u => u.Id)
            .ToListAsync(ct);
        if (users.Count == 0) return;

        var existingCount = await _db.BugReports.CountAsync(ct);
        var target = _seedOptions.ComprehensiveSeeding.MaxBugReports;
        var toCreate = Math.Max(0, target - existingCount);
        if (toCreate == 0) return;

        var rnd = Random.Shared;

        var categories = new[] { "UI/UX", "Performance", "Gameplay", "Audio", "Network", "Account", "Payment", "Other" };
        var descriptions = new[]
        {
            "The game crashes when I try to join a room.",
            "Unable to send friend requests to other players.",
            "Chat messages are not displaying correctly.",
            "Audio cuts out during gameplay.",
            "Slow loading times on the main page.",
            "Payment processing fails randomly.",
            "Cannot update profile information.",
            "Tournament brackets display incorrectly."
        };

        for (int i = 0; i < toCreate; i++)
        {
            var user = users[rnd.Next(users.Count)];
            var bug = new BugReport
            {
                Id = Guid.NewGuid(),
                UserId = user,
                Category = categories[rnd.Next(categories.Length)],
                Description = descriptions[rnd.Next(descriptions.Length)],
                ImageUrl = rnd.NextDouble() > 0.7 ? $"https://example.com/bug-screenshot-{rnd.Next(1000, 9999)}.png" : null,
                Status = (BugStatus)rnd.Next(0, 4),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-rnd.Next(1, 90))
            };

            _db.BugReports.Add(bug);
        }

        if (toCreate > 0)
            _logger.LogInformation("Seeded {Count} bug reports (idempotent to MaxBugReports).", toCreate);
    }
}
