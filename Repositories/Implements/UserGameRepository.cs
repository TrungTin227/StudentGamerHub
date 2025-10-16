using Microsoft.EntityFrameworkCore;

namespace Repositories.Implements;

/// <summary>
/// Entity Framework repository for <see cref="UserGame"/> relations.
/// </summary>
public sealed class UserGameRepository : IUserGameRepository
{
    private readonly AppDbContext _context;

    public UserGameRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IQueryable<UserGame> Query() => _context.UserGames.AsNoTracking();

    public Task<UserGame?> GetAsync(Guid userId, Guid gameId, CancellationToken ct = default)
        => _context.UserGames.FirstOrDefaultAsync(ug => ug.UserId == userId && ug.GameId == gameId, ct);

    public async Task<IReadOnlyList<UserGame>> ListByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.UserGames
            .AsNoTracking()
            .Where(ug => ug.UserId == userId)
            .OrderByDescending(ug => ug.AddedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task CreateAsync(UserGame userGame, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userGame);
        await _context.UserGames.AddAsync(userGame, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(UserGame userGame, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(userGame);
        _context.UserGames.Update(userGame);
        return Task.CompletedTask;
    }

    public async Task RemoveAsync(Guid userId, Guid gameId, CancellationToken ct = default)
    {
        var entity = await _context.UserGames.FindAsync(new object[] { userId, gameId }, ct).ConfigureAwait(false);
        if (entity is null)
            return;

        _context.UserGames.Remove(entity);
    }
}
