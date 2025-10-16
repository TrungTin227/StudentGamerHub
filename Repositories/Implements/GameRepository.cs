using Microsoft.EntityFrameworkCore;

namespace Repositories.Implements;

/// <summary>
/// Entity Framework implementation for <see cref="IGameRepository"/>.
/// </summary>
public sealed class GameRepository : IGameRepository
{
    private readonly AppDbContext _context;

    public GameRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IQueryable<Game> Query() => _context.Games.AsNoTracking();

    public Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.Games.FirstOrDefaultAsync(g => g.Id == id, ct);

    public Task<Game?> GetByNameInsensitiveAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Game name is required.", nameof(name));

        var trimmed = name.Trim();
        var query = _context.Games.AsNoTracking();

        if (_context.Database.IsNpgsql())
        {
            return query.FirstOrDefaultAsync(g => EF.Functions.ILike(g.Name, trimmed), ct);
        }

        var normalized = trimmed.ToLowerInvariant();
        return query.FirstOrDefaultAsync(g => g.Name.ToLower() == normalized, ct);
    }

    public async Task CreateAsync(Game game, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        await _context.Games.AddAsync(game, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(Game game, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        _context.Games.Update(game);
        return Task.CompletedTask;
    }

    public Task SoftDeleteAsync(Game game, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        _context.Games.Update(game);
        return Task.CompletedTask;
    }
}
