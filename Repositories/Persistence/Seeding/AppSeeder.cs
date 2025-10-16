using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Repositories.Persistence.Seeding;

/// <summary>
/// Seeds catalog data (games and sample user-games) for development/demo environments.
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

    private readonly AppDbContext _db;
    private readonly IGameRepository _games;
    private readonly IUserGameRepository _userGames;
    private readonly IGenericUnitOfWork _uow;
    private readonly ILogger<AppSeeder> _logger;

    public AppSeeder(
        AppDbContext db,
        IGameRepository games,
        IUserGameRepository userGames,
        IGenericUnitOfWork uow,
        ILogger<AppSeeder> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _userGames = userGames ?? throw new ArgumentNullException(nameof(userGames));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var result = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var existingNames = await _games.Query()
                .Select(g => g.Name)
                .ToListAsync(innerCt)
                .ConfigureAwait(false);

            var comparer = StringComparer.OrdinalIgnoreCase;
            var newGames = DefaultGames
                .Where(name => !existingNames.Contains(name, comparer))
                .Select(name => new Game
                {
                    Id = Guid.NewGuid(),
                    Name = name.Trim()
                })
                .ToList();

            if (newGames.Count > 0)
            {
                foreach (var game in newGames)
                {
                    await _games.CreateAsync(game, innerCt).ConfigureAwait(false);
                }
            }

            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

            await SeedUserGamesAsync(innerCt).ConfigureAwait(false);

            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        if (result.IsFailure)
        {
            _logger.LogWarning("App seeding failed: {Message}", result.Error.Message);
        }
    }

    private async Task SeedUserGamesAsync(CancellationToken ct)
    {
        var games = await _games.Query()
            .Select(g => new { g.Id, g.Name })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (games.Count == 0)
        {
            return;
        }

        var userIds = await _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.CreatedAtUtc)
            .Select(u => u.Id)
            .Take(10)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (userIds.Count == 0)
        {
            return;
        }

        var random = new Random(2025);

        foreach (var userId in userIds)
        {
            var ownedGameIds = await _userGames.Query()
                .Where(ug => ug.UserId == userId)
                .Select(ug => ug.GameId)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var available = games
                .Where(g => !ownedGameIds.Contains(g.Id))
                .OrderBy(_ => random.Next())
                .ToList();

            if (available.Count == 0)
            {
                continue;
            }

            var count = random.Next(1, Math.Min(3, available.Count) + 1);
            var selected = available.Take(count).ToList();

            foreach (var game in selected)
            {
                var skillIndex = random.Next(0, Enum.GetValues<GameSkillLevel>().Length);
                var inGameName = $"{game.Name.Replace(' ', '_')}#{random.Next(1000, 9999)}";

                var userGame = new UserGame
                {
                    UserId = userId,
                    GameId = game.Id,
                    InGameName = inGameName,
                    Skill = (GameSkillLevel)skillIndex,
                    AddedAt = DateTime.UtcNow.AddDays(-random.Next(0, 30)),
                    CreatedBy = userId,
                    UpdatedBy = userId
                };

                await _userGames.CreateAsync(userGame, ct).ConfigureAwait(false);
            }
        }
    }
}
