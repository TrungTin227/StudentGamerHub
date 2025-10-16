using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace Services.Implementations;

/// <summary>
/// Implementation of <see cref="IGameCatalogService"/> for managing the game catalog.
/// </summary>
public sealed class GameCatalogService : IGameCatalogService
{
    private const string GamesCacheKey = "cache:games:list:v1";
    private static readonly TimeSpan GameCacheTtl = TimeSpan.FromMinutes(20);
    private static readonly JsonSerializerOptions CacheSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] AllowedSorts = ["Id", "Name", "CreatedAtUtc"];

    private readonly IGenericUnitOfWork _uow;
    private readonly IGameRepository _games;
    private readonly IConnectionMultiplexer _redis;

    public GameCatalogService(
        IGenericUnitOfWork uow,
        IGameRepository games,
        IConnectionMultiplexer redis)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
    }

    public async Task<Result<Guid>> CreateAsync(string name, CancellationToken ct = default)
    {
        if (!TryNormalizeName(name, out var normalized, out var error))
        {
            return Result<Guid>.Failure(error);
        }

        var existing = await _games.GetByNameInsensitiveAsync(normalized, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            return Result<Guid>.Failure(new Error(Error.Codes.Conflict, $"Game '{normalized}' already exists."));
        }

        var result = await _uow.ExecuteTransactionAsync<Guid>(async innerCt =>
        {
            var entity = new Game
            {
                Id = Guid.NewGuid(),
                Name = normalized
            };

            await _games.CreateAsync(entity, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);

            return Result<Guid>.Success(entity.Id);
        }, ct: ct).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await InvalidateGameCacheAsync().ConfigureAwait(false);
        }

        return result;
    }

    public async Task<Result> RenameAsync(Guid gameId, string newName, CancellationToken ct = default)
    {
        if (gameId == Guid.Empty)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Game ID is required."));
        }

        if (!TryNormalizeName(newName, out var normalized, out var error))
        {
            return Result.Failure(error);
        }

        var game = await _games.GetByIdAsync(gameId, ct).ConfigureAwait(false);
        if (game is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Game not found."));
        }

        if (!string.Equals(game.Name, normalized, StringComparison.Ordinal))
        {
            var conflict = await _games.GetByNameInsensitiveAsync(normalized, ct).ConfigureAwait(false);
            if (conflict is not null && conflict.Id != gameId)
            {
                return Result.Failure(new Error(Error.Codes.Conflict, $"Game '{normalized}' already exists."));
            }
        }
        else
        {
            return Result.Success();
        }

        var result = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            game.Name = normalized;
            game.UpdatedAtUtc = DateTime.UtcNow;

            await _games.UpdateAsync(game, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await InvalidateGameCacheAsync().ConfigureAwait(false);
        }

        return result;
    }

    public async Task<Result> SoftDeleteAsync(Guid gameId, CancellationToken ct = default)
    {
        if (gameId == Guid.Empty)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Game ID is required."));
        }

        var game = await _games.GetByIdAsync(gameId, ct).ConfigureAwait(false);
        if (game is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Game not found."));
        }

        if (game.IsDeleted)
        {
            return Result.Success();
        }

        var result = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            game.IsDeleted = true;
            game.DeletedAtUtc = DateTime.UtcNow;

            await _games.SoftDeleteAsync(game, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await InvalidateGameCacheAsync().ConfigureAwait(false);
        }

        return result;
    }

    public async Task<Result<CursorPageResult<GameDto>>> SearchAsync(string? query, CursorRequest cursor, CancellationToken ct = default)
    {
        var sanitizedSort = NormalizeSort(cursor.SortSafe);
        if (!AllowedSorts.Contains(sanitizedSort, StringComparer.OrdinalIgnoreCase))
        {
            return Result<CursorPageResult<GameDto>>.Failure(
                new Error(Error.Codes.Validation, $"Unsupported sort '{cursor.SortSafe}'."));
        }

        var sanitizedCursor = cursor with { Sort = sanitizedSort };
        var search = _games.Query();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            search = search.Where(g => g.Name.ToLower().Contains(term));
        }
        else
        {
            await EnsureGameCacheWarmAsync(ct).ConfigureAwait(false);
        }

        CursorPageResult<Game> page;
        switch (sanitizedSort)
        {
            case "Name":
                page = await search.ToCursorPageAsync(sanitizedCursor, g => g.Name, ct).ConfigureAwait(false);
                break;
            case "CreatedAtUtc":
                page = await search.ToCursorPageAsync(sanitizedCursor, g => g.CreatedAtUtc, ct).ConfigureAwait(false);
                break;
            default:
                page = await search.ToCursorPageAsync(sanitizedCursor, g => g.Id, ct).ConfigureAwait(false);
                break;
        }

        var dtos = page.Items.Select(g => g.ToGameDto()).ToList();
        var resultPage = new CursorPageResult<GameDto>(
            Items: dtos,
            NextCursor: page.NextCursor,
            PrevCursor: page.PrevCursor,
            Size: page.Size,
            Sort: page.Sort,
            Desc: page.Desc);

        return Result<CursorPageResult<GameDto>>.Success(resultPage);
    }

    private static bool TryNormalizeName(string? name, out string normalized, out Error error)
    {
        normalized = string.Empty;
        error = default!;

        if (string.IsNullOrWhiteSpace(name))
        {
            error = new Error(Error.Codes.Validation, "Game name is required.");
            return false;
        }

        normalized = name.Trim();
        if (normalized.Length is < 1 or > 128)
        {
            error = new Error(Error.Codes.Validation, "Game name must be between 1 and 128 characters.");
            return false;
        }

        return true;
    }

    private static string NormalizeSort(string sort)
    {
        return AllowedSorts.FirstOrDefault(s => string.Equals(s, sort, StringComparison.OrdinalIgnoreCase)) ?? "Id";
    }

    private async Task EnsureGameCacheWarmAsync(CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var cached = await db.StringGetAsync(GamesCacheKey).ConfigureAwait(false);
        if (cached.HasValue)
        {
            return;
        }

        await RefreshGameCacheAsync(ct).ConfigureAwait(false);
    }

    private async Task RefreshGameCacheAsync(CancellationToken ct)
    {
        var games = await _games.Query()
            .OrderBy(g => g.Name)
            .Select(g => new GameDto(g.Id, g.Name))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var payload = JsonSerializer.Serialize(games, CacheSerializerOptions);
        await _redis.GetDatabase().StringSetAsync(GamesCacheKey, payload, GameCacheTtl).ConfigureAwait(false);
    }

    private Task InvalidateGameCacheAsync()
    {
        return _redis.GetDatabase().KeyDeleteAsync(GamesCacheKey);
    }
}
