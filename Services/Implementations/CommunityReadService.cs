namespace Services.Implementations;

/// <summary>
/// Read-side service for community discovery flows.
/// </summary>
public sealed class CommunityReadService : ICommunityReadService
{
    private readonly ICommunityQueryRepository _communityQuery;

    public CommunityReadService(ICommunityQueryRepository communityQuery)
    {
        _communityQuery = communityQuery ?? throw new ArgumentNullException(nameof(communityQuery));
    }

    public async Task<Result<PagedResult<CommunityDetailDto>>> SearchDiscoverAsync(
        Guid? currentUserId,
        string? query,
        string orderBy,
        OffsetPaging paging,
        CancellationToken ct = default)
    {
        var normalizedOrder = string.IsNullOrWhiteSpace(orderBy)
            ? "trending"
            : orderBy.Trim().ToLowerInvariant();

        if (normalizedOrder is not ("trending" or "newest"))
        {
            return Result<PagedResult<CommunityDetailDto>>.Failure(
                new Error(Error.Codes.Validation, "orderBy must be either 'trending' or 'newest'."));
        }

        var sanitizedLimit = Math.Clamp(paging.LimitSafe, 1, 50);
        var sanitizedPaging = new OffsetPaging(paging.OffsetSafe, sanitizedLimit, paging.Sort, paging.Desc);
        var request = sanitizedPaging.ToPageRequest();

        var page = await _communityQuery
            .SearchDiscoverAsync(currentUserId, query, normalizedOrder == "trending", request, ct)
            .ConfigureAwait(false);

        var items = page.Items
            .Select(model => model.ToDetailDto())
            .ToList();

        var dtoPage = new PagedResult<CommunityDetailDto>(
            items,
            page.Page,
            page.Size,
            page.TotalCount,
            page.TotalPages,
            page.HasPrevious,
            page.HasNext,
            page.Sort,
            page.Desc);

        return Result<PagedResult<CommunityDetailDto>>.Success(dtoPage);
    }
}
