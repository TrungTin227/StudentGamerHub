using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Services.Implementations;

/// <summary>
/// Read-side service for community discovery flows.
/// </summary>
public sealed class CommunityReadService : ICommunityReadService
{
    private readonly ICommunityQueryRepository _communityQuery;
    private readonly ILogger<CommunityReadService> _logger;

    public CommunityReadService(ICommunityQueryRepository communityQuery, ILogger<CommunityReadService>? logger = null)
    {
        _communityQuery = communityQuery ?? throw new ArgumentNullException(nameof(communityQuery));
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

        var sanitizedLimit = Math.Clamp(limit <= 0 ? 20 : limit, 1, 50);

        var members = await _communityQuery
            .ListRecentMembersAsync(communityId, sanitizedLimit, ct)
            .ConfigureAwait(false);

        var dtos = members
            .Select(model => model.ToCommunityMemberDto(currentUserId))
            .ToList();

        return Result<IReadOnlyList<CommunityMemberDto>>.Success(dtos);
    }
}
