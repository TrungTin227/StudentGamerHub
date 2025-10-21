using Microsoft.EntityFrameworkCore;
using Repositories.Models;

namespace Repositories.Implements;

/// <summary>
/// Club query implementation using cursor-based pagination.
/// Uses indexes on CommunityId, MembersCount for performance.
/// Respects soft-delete global filters.
/// </summary>
public sealed class ClubQueryRepository : IClubQueryRepository
{
    private readonly AppDbContext _context;

    public ClubQueryRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Search clubs within a community with filtering and cursor-based pagination.
    /// Stable sort order: MembersCount DESC, Id DESC
    /// </summary>
    public async Task<(IReadOnlyList<Club> Items, string? NextCursor)> SearchClubsAsync(
        Guid communityId,
        string? name,
        bool? isPublic,
        int? membersFrom,
        int? membersTo,
        CursorRequest cursor,
        CancellationToken ct = default)
    {
        // Base query: filter by community ID
        IQueryable<Club> query = _context.Clubs
            .AsNoTracking()
            .Where(c => c.CommunityId == communityId);

        // Apply filters
        if (!string.IsNullOrWhiteSpace(name))
        {
            var normalizedName = name.Trim().ToUpperInvariant();
            query = query.Where(c => c.Name.ToUpper().Contains(normalizedName));
        }

        if (isPublic.HasValue)
        {
            query = query.Where(c => c.IsPublic == isPublic.Value);
        }

        if (membersFrom.HasValue)
        {
            query = query.Where(c => c.MembersCount >= membersFrom.Value);
        }

        if (membersTo.HasValue)
        {
            query = query.Where(c => c.MembersCount <= membersTo.Value);
        }

        // Apply cursor-based pagination with stable sorting
        // Sort by MembersCount DESC, then Id DESC for stability
        // Use Id as the cursor key for simplicity (stable and unique)
        var orderedQuery = query
            .OrderByDescending(c => c.MembersCount)
            .ThenByDescending(c => c.Id);

        var result = await orderedQuery.ToCursorPageAsync(
            cursor,
            c => c.Id, // Use Id as cursor key (stable, unique)
            ct
        );

        return (result.Items, result.NextCursor);
    }

    /// <summary>
    /// Check if a club still has approved room members.
    /// </summary>
    public async Task<bool> HasAnyApprovedRoomsAsync(Guid clubId, CancellationToken ct = default)
    {
        return await _context.RoomMembers
            .AsNoTracking()
            .AnyAsync(rm =>
                rm.Status == RoomMemberStatus.Approved &&
                rm.Room!.ClubId == clubId,
                ct);
    }

    /// <summary>
    /// Get club by ID.
    /// </summary>
    public async Task<Club?> GetByIdAsync(Guid clubId, CancellationToken ct = default)
    {
        return await _context.Clubs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clubId, ct);
    }

    public async Task<ClubMember?> GetMemberAsync(Guid clubId, Guid userId, CancellationToken ct = default)
    {
        return await _context.ClubMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(cm => cm.ClubId == clubId && cm.UserId == userId, ct);
    }

    public async Task<ClubDetailModel?> GetDetailsAsync(Guid clubId, Guid? currentUserId, CancellationToken ct = default)
    {
        var query = _context.Clubs
            .AsNoTracking()
            .Where(c => c.Id == clubId)
            .Select(c => new ClubDetailModel(
                c.Id,
                c.CommunityId,
                c.Name,
                c.Description,
                c.IsPublic,
                c.MembersCount,
                c.Rooms.Count(),
                _context.ClubMembers
                    .Where(cm => cm.ClubId == c.Id && cm.Role == MemberRole.Owner)
                    .Select(cm => cm.UserId)
                    .FirstOrDefault(),
                currentUserId.HasValue && _context.ClubMembers
                    .Any(cm => cm.ClubId == c.Id && cm.UserId == currentUserId.Value),
                currentUserId.HasValue && _context.ClubMembers
                    .Any(cm => cm.ClubId == c.Id && cm.UserId == currentUserId.Value && cm.Role == MemberRole.Owner),
                c.CreatedAtUtc,
                c.UpdatedAtUtc
            ));

        return await query.FirstOrDefaultAsync(ct);
    }

}
