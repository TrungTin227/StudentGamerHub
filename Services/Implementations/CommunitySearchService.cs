namespace Services.Implementations;

/// <summary>
/// Community search service implementation.
/// Provides cursor-based pagination for community discovery.
/// </summary>
public sealed class CommunitySearchService : ICommunitySearchService
{
    private readonly ICommunityQueryRepository _communityQuery;

    public CommunitySearchService(ICommunityQueryRepository communityQuery)
    {
        _communityQuery = communityQuery ?? throw new ArgumentNullException(nameof(communityQuery));
    }

    /// <inheritdoc/>
    public async Task<Result<CursorPageResult<CommunityBriefDto>>> SearchAsync(
        string? school,
        Guid? gameId,
        bool? isPublic,
        int? membersFrom,
        int? membersTo,
        CursorRequest cursor,
        CancellationToken ct = default)
    {
        // Validate members range
        if (membersFrom.HasValue && membersFrom.Value < 0)
            return Result<CursorPageResult<CommunityBriefDto>>.Failure(
                new Error(Error.Codes.Validation, "membersFrom must be non-negative."));

        if (membersTo.HasValue && membersTo.Value < 0)
            return Result<CursorPageResult<CommunityBriefDto>>.Failure(
                new Error(Error.Codes.Validation, "membersTo must be non-negative."));

        if (membersFrom.HasValue && membersTo.HasValue && membersFrom.Value > membersTo.Value)
            return Result<CursorPageResult<CommunityBriefDto>>.Failure(
                new Error(Error.Codes.Validation, "membersFrom cannot be greater than membersTo."));

        // Call repository
        var (items, nextCursor) = await _communityQuery.SearchCommunitiesAsync(
            school,
            gameId,
            isPublic,
            membersFrom,
            membersTo,
            cursor,
            ct);

        // Map to DTOs
        var dtos = items.Select(c => c.ToBriefDto()).ToList();

        // Create cursor page result
        var result = new CursorPageResult<CommunityBriefDto>(
            Items: dtos,
            NextCursor: nextCursor,
            PrevCursor: null, // Not implemented in this version
            Size: cursor.SizeSafe,
            Sort: cursor.SortSafe,
            Desc: cursor.Desc
        );

        return Result<CursorPageResult<CommunityBriefDto>>.Success(result);
    }
}
