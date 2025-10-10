using Microsoft.EntityFrameworkCore;

namespace Repositories.Implements;

/// <summary>
/// Club command implementation for write operations.
/// Does NOT manage transactions - caller must use ExecuteTransactionAsync.
/// </summary>
public sealed class ClubCommandRepository : IClubCommandRepository
{
    private readonly AppDbContext _context;

    public ClubCommandRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Create a new club.
    /// Does not commit - transaction is managed by UnitOfWork.
    /// </summary>
    public async Task CreateAsync(Club club, CancellationToken ct = default)
    {
        await _context.Clubs.AddAsync(club, ct);
    }
}
