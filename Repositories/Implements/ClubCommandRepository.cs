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

    /// <summary>
    /// Update existing club entity.
    /// </summary>
    public Task UpdateAsync(Club club, CancellationToken ct = default)
    {
        _context.Clubs.Update(club);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Soft delete (archive) a club.
    /// </summary>
    public async Task SoftDeleteAsync(Guid clubId, Guid deletedBy, CancellationToken ct = default)
    {
        var club = await _context.Clubs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == clubId, ct)
            .ConfigureAwait(false);

        if (club is null)
            return;

        var now = DateTime.UtcNow;
        club.IsDeleted = true;
        club.DeletedAtUtc = now;
        club.DeletedBy = deletedBy;
        club.UpdatedAtUtc = now;
        club.UpdatedBy = deletedBy;

        _context.Clubs.Update(club);
    }

    public Task AddMemberAsync(ClubMember member, CancellationToken ct = default)
    {
        return _context.ClubMembers.AddAsync(member, ct).AsTask();
    }

    public async Task RemoveMemberAsync(Guid clubId, Guid userId, CancellationToken ct = default)
    {
        var entity = await _context.ClubMembers
            .FirstOrDefaultAsync(cm => cm.ClubId == clubId && cm.UserId == userId, ct);

        if (entity is not null)
        {
            _context.ClubMembers.Remove(entity);
        }
    }
}
