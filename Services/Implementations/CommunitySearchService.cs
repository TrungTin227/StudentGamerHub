namespace Services.Implementations;

/// <summary>
/// Community search service implementation.
/// Provides offset-based pagination for community discovery.
/// </summary>
public sealed class CommunitySearchService : ICommunitySearchService
{
    private readonly ICommunityQueryRepository _communityQuery;

    public CommunitySearchService(ICommunityQueryRepository communityQuery)
    {
        _communityQuery = communityQuery ?? throw new ArgumentNullException(nameof(communityQuery));
    }

    /// <inheritdoc/>
    public async Task<Result<PagedResult<CommunityBriefDto>>> SearchAsync(
        string? school,
        Guid? gameId,
        bool? isPublic,
        int? membersFrom,
        int? membersTo,
        PageRequest paging,
        CancellationToken ct = default)
    {
        var validationResult = ValidateSearchParameters(membersFrom, membersTo);
        if (!validationResult.IsSuccess)
        {
            return Result<PagedResult<CommunityBriefDto>>.Failure(validationResult.Error);
        }

        var pagedResult = await _communityQuery.SearchCommunitiesAsync(
            school,
            gameId,
            isPublic,
            membersFrom,
            membersTo,
            paging,
            ct);

        var dtos = pagedResult.Items.Select(c => c.ToBriefDto()).ToList();

        var result = new PagedResult<CommunityBriefDto>(
            dtos,
            pagedResult.Page,
            pagedResult.Size,
            pagedResult.TotalCount,
            pagedResult.TotalPages,
            pagedResult.HasPrevious,
            pagedResult.HasNext,
            pagedResult.Sort,
            pagedResult.Desc
        );

        return Result<PagedResult<CommunityBriefDto>>.Success(result);
    }

    private static Result ValidateSearchParameters(int? membersFrom, int? membersTo)
    {
        if (membersFrom.HasValue && membersFrom.Value < 0)
            return Result.Failure(
                new Error(Error.Codes.Validation, "membersFrom must be non-negative."));

        if (membersTo.HasValue && membersTo.Value < 0)
            return Result.Failure(
                new Error(Error.Codes.Validation, "membersTo must be non-negative."));

        if (membersFrom.HasValue && membersTo.HasValue && membersFrom.Value > membersTo.Value)
            return Result.Failure(
                new Error(Error.Codes.Validation, "membersFrom cannot be greater than membersTo."));

        return Result.Success();
    }
}
