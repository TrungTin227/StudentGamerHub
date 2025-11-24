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
/// 6. Wrap in PagedResult
/// 
/// NOTE: Online status is real-time and may change between page requests.
/// This is acceptable per requirements.
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

    public async Task<Result<PagedResult<TeammateDto>>> SearchAsync(
        Guid currentUserId,
        Guid? gameId,
        string? university,
        GameSkillLevel? skill,
        bool onlineOnly,
        PageRequest paging,
        CancellationToken ct = default)
    {
        // Step 1: Call repository with filters
        var filter = new TeammateSearchFilter(gameId, university, skill);
        var pagedCandidates = await _teammateQueries
            .SearchCandidatesAsync(currentUserId, filter, paging, ct);

        if (pagedCandidates.Items.Count == 0)
        {
            return Result<PagedResult<TeammateDto>>.Success(
                new PagedResult<TeammateDto>(
                    Array.Empty<TeammateDto>(),
                    pagedCandidates.Page,
                    pagedCandidates.Size,
                    pagedCandidates.TotalCount,
                    pagedCandidates.TotalPages,
                    pagedCandidates.HasPrevious,
                    pagedCandidates.HasNext,
                    pagedCandidates.Sort,
                    pagedCandidates.Desc
                ));
        }

        // Step 2: Batch check online status (1 Redis pipeline)
        var userIds = pagedCandidates.Items.Select(c => c.UserId).ToArray();
        var presenceResult = await _presence.BatchIsOnlineAsync(userIds, ct);

        if (!presenceResult.IsSuccess)
        {
            return Result<PagedResult<TeammateDto>>.Failure(presenceResult.Error);
        }

        var onlineMap = presenceResult.Value;

        // Step 3: Map candidates to DTOs with online status
        var items = pagedCandidates.Items.Select(c => new
        {
            Dto = new TeammateDto
            {
                User = new UserBriefDto
                {
                    Id = c.UserId,
                    UserName = c.FullName ?? c.UserId.ToString(),
                    FullName = c.FullName,
                    AvatarUrl = c.AvatarUrl,
                    Level = 0
                },
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
            .OrderByDescending(x => x.Dto.IsOnline)
            .ThenByDescending(x => x.Points)
            .ThenByDescending(x => x.Dto.SharedGames)
            .ThenByDescending(x => x.UserId)
            .Select(x => x.Dto)
            .ToList();

        // Step 6: Wrap in PagedResult
        var result = new PagedResult<TeammateDto>(
            sorted,
            pagedCandidates.Page,
            pagedCandidates.Size,
            pagedCandidates.TotalCount,
            pagedCandidates.TotalPages,
            pagedCandidates.HasPrevious,
            pagedCandidates.HasNext,
            pagedCandidates.Sort,
            pagedCandidates.Desc
        );

        return Result<PagedResult<TeammateDto>>.Success(result);
    }
}
