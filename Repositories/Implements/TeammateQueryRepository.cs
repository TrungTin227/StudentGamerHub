using Microsoft.EntityFrameworkCore;

namespace Repositories.Implements;

/// <summary>
/// Teammate search queries implementation.
/// Uses indexed columns (User.University, UserGame.GameId, UserGame.Skill) for efficient filtering.
/// Computes SharedGames by comparing current user's game list with candidates.
/// Stable sorting: Points DESC, SharedGames DESC, UserId DESC (for deterministic pagination).
/// </summary>
public sealed class TeammateQueryRepository : ITeammateQueryRepository
{
    private readonly AppDbContext _context;

    public TeammateQueryRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PagedResult<TeammateCandidate>> SearchCandidatesAsync(
        Guid currentUserId,
        TeammateSearchFilter filter,
        PageRequest paging,
        CancellationToken ct = default)
    {
        // 1) Get current user's game IDs for SharedGames calculation
        var myGameIds = await _context.UserGames
            .Where(ug => ug.UserId == currentUserId)
            .Select(ug => ug.GameId)
            .Distinct()
            .ToListAsync(ct);

        // 2) Base query: users who are not soft-deleted and not the current user
        var baseQuery = _context.Users
            .AsNoTracking()
            .Where(u => u.Id != currentUserId);

        // 3) Apply filters by joining with UserGames when needed
        IQueryable<User> filteredUsers;

        if (filter.GameId.HasValue || filter.Skill.HasValue)
        {
            filteredUsers = baseQuery
                .Where(u => u.UserGames.Any(ug =>
                    (!filter.GameId.HasValue || ug.GameId == filter.GameId.Value) &&
                    (!filter.Skill.HasValue || ug.Skill == filter.Skill.Value)
                ));
        }
        else
        {
            filteredUsers = baseQuery;
        }

        // 4) Apply university filter (indexed column)
        if (!string.IsNullOrWhiteSpace(filter.University))
        {
            filteredUsers = filteredUsers.Where(u => u.University == filter.University);
        }

        // 5) Project to intermediate result with SharedGames calculation
        var projected = filteredUsers
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.AvatarUrl,
                u.University,
                u.Points,
                SharedGames = u.UserGames
                    .Where(ug => myGameIds.Contains(ug.GameId))
                    .Select(ug => ug.GameId)
                    .Distinct()
                    .Count()
            });

        // 6) Get total count
        var total = await projected.CountAsync(ct);

        // 7) Stable sorting: Points DESC, SharedGames DESC, UserId DESC
        var ordered = projected
            .OrderByDescending(x => x.Points)
            .ThenByDescending(x => x.SharedGames)
            .ThenByDescending(x => x.Id);

        // 8) Offset pagination
        var page = paging.PageSafe;
        var size = Math.Clamp(paging.SizeSafe, 1, 200);
        var skip = (page - 1) * size;

        var items = await ordered
            .Skip(skip)
            .Take(size)
            .ToListAsync(ct);

        // 9) Map to TeammateCandidate
        var candidates = items.Select(x => new TeammateCandidate(
            UserId: x.Id,
            FullName: x.FullName,
            AvatarUrl: x.AvatarUrl,
            University: x.University,
            Points: x.Points,
            SharedGames: x.SharedGames
        )).ToList();

        // 10) Build paged result
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)size);
        var hasPrev = page > 1 && total > 0;
        var hasNext = totalPages > 0 && page < totalPages;

        return new PagedResult<TeammateCandidate>(
            candidates,
            page,
            size,
            total,
            totalPages,
            hasPrev,
            hasNext,
            "Points",
            true);
    }
}
