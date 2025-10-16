using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Globalization;
using System.Text.Json;

namespace Services.Implementations;

/// <summary>
/// Implementation of <see cref="IUserGameService"/> allowing users to manage their games.
/// </summary>
public sealed class UserGameService : IUserGameService
{
    private const int MaxGamesPerUser = 20;
    private const string GamesCacheKey = "cache:games:list:v1";
    private const string UserGamesCacheKeyPrefix = "cache:user:{0}:games";
    private static readonly TimeSpan GameCacheTtl = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan UserGamesCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions CacheSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IGenericUnitOfWork _uow;
    private readonly IUserGameRepository _userGames;
    private readonly IGameRepository _games;
    private readonly IConnectionMultiplexer _redis;

    public UserGameService(
        IGenericUnitOfWork uow,
        IUserGameRepository userGames,
        IGameRepository games,
        IConnectionMultiplexer redis)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _userGames = userGames ?? throw new ArgumentNullException(nameof(userGames));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
    }

    public async Task<Result> AddAsync(Guid currentUserId, Guid gameId, string? inGameName, GameSkillLevel? skill, CancellationToken ct = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Current user is required."));
        }

        if (gameId == Guid.Empty)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Game ID is required."));
        }

        if (!TryNormalizeInGameName(inGameName, out var normalizedName, out var error))
        {
            return Result.Failure(error);
        }

        if (!IsValidSkill(skill))
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Skill level is invalid."));
        }

        var game = await _games.GetByIdAsync(gameId, ct).ConfigureAwait(false);
        if (game is null || game.IsDeleted)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Game not found."));
        }

        var existing = await _userGames.GetAsync(currentUserId, gameId, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            return Result.Failure(new Error(Error.Codes.Conflict, "Game already added."));
        }

        var count = await _userGames.Query()
            .Where(ug => ug.UserId == currentUserId)
            .CountAsync(ct)
            .ConfigureAwait(false);

        if (count >= MaxGamesPerUser)
        {
            return Result.Failure(new Error(Error.Codes.Validation, $"Users can track at most {MaxGamesPerUser} games."));
        }

        var result = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var entity = new UserGame
            {
                UserId = currentUserId,
                GameId = gameId,
                InGameName = normalizedName,
                Skill = skill,
                AddedAt = DateTime.UtcNow,
                CreatedBy = currentUserId,
                UpdatedBy = currentUserId
            };

            await _userGames.CreateAsync(entity, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await InvalidateUserGamesCacheAsync(currentUserId).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<Result> UpdateAsync(Guid currentUserId, Guid gameId, string? inGameName, GameSkillLevel? skill, CancellationToken ct = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Current user is required."));
        }

        if (gameId == Guid.Empty)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Game ID is required."));
        }

        if (!TryNormalizeInGameName(inGameName, out var normalizedName, out var error))
        {
            return Result.Failure(error);
        }

        if (!IsValidSkill(skill))
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Skill level is invalid."));
        }

        var userGame = await _userGames.GetAsync(currentUserId, gameId, ct).ConfigureAwait(false);
        if (userGame is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "User game not found."));
        }

        var result = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            userGame.InGameName = normalizedName;
            userGame.Skill = skill;
            userGame.UpdatedAtUtc = DateTime.UtcNow;
            userGame.UpdatedBy = currentUserId;

            await _userGames.UpdateAsync(userGame, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await InvalidateUserGamesCacheAsync(currentUserId).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<Result> RemoveAsync(Guid currentUserId, Guid gameId, CancellationToken ct = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Current user is required."));
        }

        if (gameId == Guid.Empty)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Game ID is required."));
        }

        var result = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var entity = await _userGames.GetAsync(currentUserId, gameId, innerCt).ConfigureAwait(false);
            if (entity is null)
            {
                return Result.Success();
            }

            await _userGames.RemoveAsync(currentUserId, gameId, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await InvalidateUserGamesCacheAsync(currentUserId).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<Result<IReadOnlyList<UserGameDto>>> ListMineAsync(Guid currentUserId, CancellationToken ct = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return Result<IReadOnlyList<UserGameDto>>.Failure(new Error(Error.Codes.Validation, "Current user is required."));
        }

        var cacheKey = string.Format(CultureInfo.InvariantCulture, UserGamesCacheKeyPrefix, currentUserId);
        var db = _redis.GetDatabase();
        var cached = await db.StringGetAsync(cacheKey).ConfigureAwait(false);
        if (cached.HasValue)
        {
            var cachedDtos = JsonSerializer.Deserialize<List<UserGameDto>>(cached.ToString(), CacheSerializerOptions) ?? new List<UserGameDto>();
            return Result<IReadOnlyList<UserGameDto>>.Success(cachedDtos);
        }

        var items = await _userGames.ListByUserAsync(currentUserId, ct).ConfigureAwait(false);
        if (items.Count == 0)
        {
            await db.StringSetAsync(cacheKey, "[]", UserGamesCacheTtl).ConfigureAwait(false);
            return Result<IReadOnlyList<UserGameDto>>.Success(Array.Empty<UserGameDto>());
        }

        var gameIds = items.Select(i => i.GameId).Distinct().ToList();
        var gameNames = await GetGameNamesAsync(gameIds, ct).ConfigureAwait(false);

        var dtos = items
            .Select(item => item.ToUserGameDto(gameNames.TryGetValue(item.GameId, out var name) ? name : ""))
            .ToList();

        var payload = JsonSerializer.Serialize(dtos, CacheSerializerOptions);
        await db.StringSetAsync(cacheKey, payload, UserGamesCacheTtl).ConfigureAwait(false);

        return Result<IReadOnlyList<UserGameDto>>.Success(dtos);
    }

    private static bool TryNormalizeInGameName(string? value, out string? normalized, out Error error)
    {
        normalized = null;
        error = default!;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        normalized = value.Trim();
        if (normalized.Length is < 1 or > 64)
        {
            error = new Error(Error.Codes.Validation, "In-game name must be between 1 and 64 characters when provided.");
            normalized = null;
            return false;
        }

        return true;
    }

    private static bool IsValidSkill(GameSkillLevel? skill)
    {
        if (!skill.HasValue)
        {
            return true;
        }

        return Enum.IsDefined(typeof(GameSkillLevel), skill.Value);
    }

    private async Task<Dictionary<Guid, string>> GetGameNamesAsync(IEnumerable<Guid> gameIds, CancellationToken ct)
    {
        var required = gameIds.Distinct().ToList();
        if (required.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var db = _redis.GetDatabase();
        var cached = await db.StringGetAsync(GamesCacheKey).ConfigureAwait(false);
        Dictionary<Guid, string>? names = null;

        if (cached.HasValue)
        {
            var cachedGames = JsonSerializer.Deserialize<List<GameDto>>(cached.ToString(), CacheSerializerOptions) ?? new List<GameDto>();
            names = cachedGames.ToDictionary(g => g.Id, g => g.Name);
            if (required.All(id => names.ContainsKey(id)))
            {
                return names;
            }
        }

        var refreshed = await _games.Query()
            .Select(g => new GameDto(g.Id, g.Name))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var payload = JsonSerializer.Serialize(refreshed, CacheSerializerOptions);
        await db.StringSetAsync(GamesCacheKey, payload, GameCacheTtl).ConfigureAwait(false);

        return refreshed.ToDictionary(g => g.Id, g => g.Name);
    }

    private Task InvalidateUserGamesCacheAsync(Guid userId)
    {
        var cacheKey = string.Format(CultureInfo.InvariantCulture, UserGamesCacheKeyPrefix, userId);
        return _redis.GetDatabase().KeyDeleteAsync(cacheKey);
    }
}
