namespace Repositories.Interfaces;

/// <summary>
/// Query interface for Club entity - search and filtering
/// </summary>
public interface IClubQueryRepository
{
    /// <summary>
    /// Search clubs within a community with filtering by name, visibility, and member count range.
    /// Uses cursor-based pagination with stable sorting by (MembersCount DESC, Id DESC).
    /// </summary>
    /// <param name="communityId">Community ID to search within</param>
    /// <param name="name">Filter by club name (case-insensitive, partial match)</param>
    /// <param name="isPublic">Filter by public/private status (null = all)</param>
    /// <param name="membersFrom">Minimum members count (inclusive)</param>
    /// <param name="membersTo">Maximum members count (inclusive)</param>
    /// <param name="cursor">Cursor pagination request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of clubs and next cursor</returns>
    Task<(IReadOnlyList<Club> Items, string? NextCursor)> SearchClubsAsync(
        Guid communityId,
        string? name,
        bool? isPublic,
        int? membersFrom,
        int? membersTo,
        CursorRequest cursor,
        CancellationToken ct = default);

    /// <summary>
    /// Get club by ID.
    /// </summary>
    /// <param name="clubId">Club ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Club entity or null if not found</returns>
    Task<Club?> GetByIdAsync(Guid clubId, CancellationToken ct = default);
}
