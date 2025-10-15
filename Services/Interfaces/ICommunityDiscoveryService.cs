using BusinessObjects.Common.Results;
using DTOs.Communities;

namespace Services.Interfaces;

/// <summary>
/// Service for discovering popular communities with filtering and cursor-based pagination.
/// Implements popularity scoring based on:
/// 1. MembersCount (DESC)
/// 2. RecentActivity48h (DESC) - number of room joins in last 48 hours
/// 3. CreatedAtUtc (DESC)
/// 4. Id (ASC) - for stable tie-breaking
/// </summary>
public interface ICommunityDiscoveryService
{
    /// <summary>
    /// Discover popular communities with filtering and stable cursor-based pagination.
    /// </summary>
    /// <param name="school">Filter by school (case-insensitive exact match)</param>
    /// <param name="gameId">Filter communities that include this game</param>
    /// <param name="cursor">Cursor token for pagination (null = first page)</param>
    /// <param name="size">Page size (clamped 1-100, default 20)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Discovery response with items and next cursor</returns>
    Task<Result<DiscoverResponse>> DiscoverAsync(
        string? school,
        Guid? gameId,
        string? cursor,
        int? size,
        CancellationToken ct = default);
}
