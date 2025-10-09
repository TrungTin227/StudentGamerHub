using Microsoft.EntityFrameworkCore;

namespace Repositories.Implements;

/// <summary>
/// FriendLink query implementation for Dashboard feature
/// Uses AppDbContext for read-only queries
/// Respects soft-delete global filters
/// </summary>
public sealed class FriendLinkQueriesRepository : IFriendLinkQuerRepository
{
    private readonly AppDbContext _context;

    public FriendLinkQueriesRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get list of accepted friend IDs for current user
    /// Returns the "other person" ID from FriendLink where Status=Accepted
    /// (If currentUserId is Sender, return RecipientId; if Recipient, return SenderId)
    /// </summary>
    public async Task<IReadOnlyList<Guid>> GetAcceptedFriendIdsAsync(
        Guid currentUserId, 
        CancellationToken ct = default)
    {
        // Query only the necessary IDs, no entity loading
        // Global soft-delete filter is automatically applied
        var friendIds = await _context.FriendLinks
            .AsNoTracking()
            .Where(link => link.Status == FriendStatus.Accepted)
            .Where(link => link.SenderId == currentUserId || link.RecipientId == currentUserId)
            .Select(link => link.SenderId == currentUserId 
                ? link.RecipientId 
                : link.SenderId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return friendIds;
    }
}
