using Application.Friends;
using DTOs.Teammates;

namespace Services.Implementations;

/// <summary>
/// Service implementation for teammate search.
/// Pipeline:
/// 1. Call repository with filters
/// 2. Batch check online status (1 Redis pipeline)
/// 3. Filter by onlineOnly if needed
/// 4. Map to DTOs with presence info
/// 5. Sort: online DESC ? points DESC ? sharedGames DESC ? userId DESC
/// 6. Wrap in CursorPageResult
/// 
/// NOTE: Cursor may exhibit "jitter" when online status changes between pages.
/// This is acceptable per requirements as online status is real-time.
/// </summary>
public sealed class TeammateFinderService : ITeammateFinderService
{
    private readonly ITeammateQueryRepository _teammateQueries;
    private readonly IPresenceService _presence;

    public TeammateFinderService(
        ITeammateQueryRepository teammateQueries,
        IPresenceService presence)
    {
        _teammateQueries = teammateQueries ?? throw new ArgumentNullException(nameof(teammateQueries));
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
    }

    public async Task<Result<CursorPageResult<TeammateDto>>> SearchAsync(
        Guid currentUserId,
        Guid? gameId,
        string? university,
        GameSkillLevel? skill,
        bool onlineOnly,
        CursorRequest cursor,
        CancellationToken ct = default)
    {
        // Step 1: Call repository with filters
        var filter = new TeammateSearchFilter(gameId, university, skill);
        var (candidates, nextCursor) = await _teammateQueries
            .SearchCandidatesAsync(currentUserId, filter, cursor, ct);

        if (candidates.Count == 0)
        {
            // Early return for empty results
            return Result<CursorPageResult<TeammateDto>>.Success(
                new CursorPageResult<TeammateDto>(
                    Items: Array.Empty<TeammateDto>(),
                    NextCursor: null,
                    PrevCursor: null,
                    Size: cursor.SizeSafe,
                    Sort: cursor.SortSafe,
                    Desc: cursor.Desc
                ));
        }

        // Step 2: Batch check online status (1 Redis pipeline)
        var userIds = candidates.Select(c => c.UserId).ToArray();
        var presenceResult = await _presence.BatchIsOnlineAsync(userIds, ct);

        if (!presenceResult.IsSuccess)
        {
            return Result<CursorPageResult<TeammateDto>>.Failure(presenceResult.Error);
        }

        var onlineMap = presenceResult.Value;

        // Step 3: Map candidates to DTOs with online status
        var items = candidates.Select(c => new
        {
            Dto = new TeammateDto
            {
                User = new UserBriefDto(
                    Id: c.UserId,
                    UserName: c.FullName ?? c.UserId.ToString(), // Fallback to ID if no name
                    AvatarUrl: c.AvatarUrl
                ),
                IsOnline = onlineMap.TryGetValue(c.UserId, out var online) && online,
                SharedGames = c.SharedGames
            },
            Points = c.Points,
            UserId = c.UserId
        }).ToList();

        // Step 4: Filter by onlineOnly if needed
        if (onlineOnly)
        {
            items = items.Where(x => x.Dto.IsOnline).ToList();
        }

        // Step 5: Sort with online status priority
        // Sort order: online DESC ? points DESC ? sharedGames DESC ? userId DESC
        var sorted = items
            .OrderByDescending(x => x.Dto.IsOnline)  // Online first
            .ThenByDescending(x => x.Points)          // Then by points
            .ThenByDescending(x => x.Dto.SharedGames) // Then by shared games
            .ThenByDescending(x => x.UserId)          // Finally by user ID for stability
            .Select(x => x.Dto)
            .ToList();

        // Step 6: Wrap in CursorPageResult
        // NOTE: nextCursor from repo is based on (points, sharedGames, userId) only.
        // When online status changes, pagination may show slight inconsistency.
        // This is acceptable as online status is ephemeral real-time data.
        var result = new CursorPageResult<TeammateDto>(
            Items: sorted,
            NextCursor: nextCursor,
            PrevCursor: null, // Repository doesn't provide prev cursor
            Size: cursor.SizeSafe,
            Sort: cursor.SortSafe,
            Desc: cursor.Desc
        );

        return Result<CursorPageResult<TeammateDto>>.Success(result);
    }
}
