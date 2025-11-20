using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Services.Common.Caching;

namespace Services.Implementations;

/// <summary>
/// Read-side service for community discovery flows with Redis caching.
/// </summary>
public sealed class CommunityReadService : ICommunityReadService
{
    private static readonly TimeSpan CommunityCacheTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MembersCacheTtl = TimeSpan.FromMinutes(5);

    private readonly ICommunityQueryRepository _communityQuery;
    private readonly ICacheService _cache;
    private readonly ILogger<CommunityReadService> _logger;

    public CommunityReadService(
        ICommunityQueryRepository communityQuery,
        ICacheService cache,
        ILogger<CommunityReadService>? logger = null)
    {
        _communityQuery = communityQuery ?? throw new ArgumentNullException(nameof(communityQuery));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? NullLogger<CommunityReadService>.Instance;
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

        // ? Cache key includes currentUserId for personalized results (IsMember, IsOwner)
        var cacheKey = $"community:discover:user:{currentUserId}:q:{query}:order:{normalizedOrder}:offset:{paging.OffsetSafe}:limit:{sanitizedLimit}";

        var cached = await _cache.GetAsync<PagedResult<CommunityDetailDto>>(cacheKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            _logger.LogDebug("Community discover cache HIT for query: {Query}", query);
            return Result<PagedResult<CommunityDetailDto>>.Success(cached);
        }

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

        // Cache for 10 minutes
        await _cache.SetAsync(cacheKey, dtoPage, CommunityCacheTtl, ct).ConfigureAwait(false);
        _logger.LogDebug("Community discover cached for query: {Query}", query);

        return Result<PagedResult<CommunityDetailDto>>.Success(dtoPage);
    }

    public async Task<Result<OffsetPage<CommunityMemberDto>>> ListMembersAsync(
        Guid communityId,
        MemberListFilter filter,
        OffsetPaging paging,
        Guid? currentUserId,
        CancellationToken ct = default)
    {
        if (communityId == Guid.Empty)
        {
            return Result<OffsetPage<CommunityMemberDto>>.Failure(
                new Error(Error.Codes.Validation, "CommunityId is required."));
        }

        ArgumentNullException.ThrowIfNull(filter);

        // ? Cache key for member list
        var cacheKey = $"community:members:{communityId}:role:{filter.Role}:q:{filter.Query}:sort:{filter.Sort}:offset:{paging.OffsetSafe}:limit:{paging.LimitSafe}:user:{currentUserId}";

        var cached = await _cache.GetAsync<OffsetPage<CommunityMemberDto>>(cacheKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            _logger.LogDebug("Community members cache HIT for community: {CommunityId}", communityId);
            return Result<OffsetPage<CommunityMemberDto>>.Success(cached);
        }

        var community = await _communityQuery.GetByIdAsync(communityId, ct).ConfigureAwait(false);
        if (community is null)
        {
            return Result<OffsetPage<CommunityMemberDto>>.Failure(
                new Error(Error.Codes.NotFound, "Community not found."));
        }

        var isMember = false;
        if (currentUserId.HasValue)
        {
            var membership = await _communityQuery
                .GetMemberAsync(communityId, currentUserId.Value, ct)
                .ConfigureAwait(false);
            isMember = membership is not null;
        }

        if (!community.IsPublic && !isMember)
        {
            return Result<OffsetPage<CommunityMemberDto>>.Failure(
                new Error(Error.Codes.Forbidden, "CommunityViewRestricted"));
        }

        if (community.IsPublic && !isMember)
        {
            _logger.LogInformation(
                "Non-member user {UserId} viewed members list for public community {CommunityId}.",
                currentUserId ?? Guid.Empty,
                communityId);
        }

        var sanitizedLimit = Math.Clamp(paging.LimitSafe, 1, 50);
        var sanitizedPaging = new OffsetPaging(paging.OffsetSafe, sanitizedLimit, paging.Sort, paging.Desc);

        var page = await _communityQuery
            .ListMembersAsync(communityId, filter, sanitizedPaging, ct)
            .ConfigureAwait(false);

        var dtoPage = page.Map(model => model.ToCommunityMemberDto(currentUserId));

        // Cache for 5 minutes
        await _cache.SetAsync(cacheKey, dtoPage, MembersCacheTtl, ct).ConfigureAwait(false);
        _logger.LogDebug("Community members cached for community: {CommunityId}", communityId);

        return Result<OffsetPage<CommunityMemberDto>>.Success(dtoPage);
    }

    public async Task<Result<IReadOnlyList<CommunityMemberDto>>> ListRecentMembersAsync(
        Guid communityId,
        int limit,
        Guid? currentUserId,
        CancellationToken ct = default)
    {
        if (communityId == Guid.Empty)
        {
            return Result<IReadOnlyList<CommunityMemberDto>>.Failure(
                new Error(Error.Codes.Validation, "CommunityId is required."));
        }

        // ? Cache key for recent members
        var sanitizedLimit = Math.Clamp(limit <= 0 ? 20 : limit, 1, 50);
        var cacheKey = $"community:recentmembers:{communityId}:limit:{sanitizedLimit}:user:{currentUserId}";

        var cached = await _cache.GetAsync<IReadOnlyList<CommunityMemberDto>>(cacheKey, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            _logger.LogDebug("Recent members cache HIT for community: {CommunityId}", communityId);
            return Result<IReadOnlyList<CommunityMemberDto>>.Success(cached);
        }

        var community = await _communityQuery.GetByIdAsync(communityId, ct).ConfigureAwait(false);
        if (community is null)
        {
            return Result<IReadOnlyList<CommunityMemberDto>>.Failure(
                new Error(Error.Codes.NotFound, "Community not found."));
        }

        var isMember = false;
        if (currentUserId.HasValue)
        {
            var membership = await _communityQuery
                .GetMemberAsync(communityId, currentUserId.Value, ct)
                .ConfigureAwait(false);
            isMember = membership is not null;
        }

        if (!community.IsPublic && !isMember)
        {
            return Result<IReadOnlyList<CommunityMemberDto>>.Failure(
                new Error(Error.Codes.Forbidden, "CommunityViewRestricted"));
        }

        if (community.IsPublic && !isMember)
        {
            _logger.LogInformation(
                "Non-member user {UserId} viewed recent members for public community {CommunityId}.",
                currentUserId ?? Guid.Empty,
                communityId);
        }

        var members = await _communityQuery
            .ListRecentMembersAsync(communityId, sanitizedLimit, ct)
            .ConfigureAwait(false);

        var dtos = members
            .Select(model => model.ToCommunityMemberDto(currentUserId))
            .ToList();

        // Cache for 5 minutes
        await _cache.SetAsync<IReadOnlyList<CommunityMemberDto>>(cacheKey, dtos, MembersCacheTtl, ct).ConfigureAwait(false);
        _logger.LogDebug("Recent members cached for community: {CommunityId}", communityId);

        return Result<IReadOnlyList<CommunityMemberDto>>.Success(dtos);
    }
}
