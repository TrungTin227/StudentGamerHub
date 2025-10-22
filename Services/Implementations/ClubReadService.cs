namespace Services.Implementations;

/// <summary>
/// Read-only queries for club membership directories.
/// </summary>
public sealed class ClubReadService : IClubReadService
{
    private readonly IClubQueryRepository _clubQuery;

    public ClubReadService(IClubQueryRepository clubQuery)
    {
        _clubQuery = clubQuery ?? throw new ArgumentNullException(nameof(clubQuery));
    }

    public async Task<Result<OffsetPage<ClubMemberDto>>> ListMembersAsync(
        Guid clubId,
        MemberListFilter filter,
        OffsetPaging paging,
        Guid? currentUserId,
        CancellationToken ct = default)
    {
        if (clubId == Guid.Empty)
        {
            return Result<OffsetPage<ClubMemberDto>>.Failure(
                new Error(Error.Codes.Validation, "ClubId is required."));
        }

        ArgumentNullException.ThrowIfNull(filter);

        var club = await _clubQuery.GetByIdAsync(clubId, ct).ConfigureAwait(false);
        if (club is null)
        {
            return Result<OffsetPage<ClubMemberDto>>.Failure(
                new Error(Error.Codes.NotFound, "Club not found."));
        }

        var sanitizedLimit = Math.Clamp(paging.LimitSafe, 1, 50);
        var sanitizedPaging = new OffsetPaging(paging.OffsetSafe, sanitizedLimit, paging.Sort, paging.Desc);

        var page = await _clubQuery
            .ListMembersAsync(clubId, filter, sanitizedPaging, ct)
            .ConfigureAwait(false);

        var dtoPage = page.Map(model => model.ToClubMemberDto(currentUserId));

        return Result<OffsetPage<ClubMemberDto>>.Success(dtoPage);
    }

    public async Task<Result<IReadOnlyList<ClubMemberDto>>> ListRecentMembersAsync(
        Guid clubId,
        int limit,
        Guid? currentUserId,
        CancellationToken ct = default)
    {
        if (clubId == Guid.Empty)
        {
            return Result<IReadOnlyList<ClubMemberDto>>.Failure(
                new Error(Error.Codes.Validation, "ClubId is required."));
        }

        var club = await _clubQuery.GetByIdAsync(clubId, ct).ConfigureAwait(false);
        if (club is null)
        {
            return Result<IReadOnlyList<ClubMemberDto>>.Failure(
                new Error(Error.Codes.NotFound, "Club not found."));
        }

        var sanitizedLimit = Math.Clamp(limit <= 0 ? 20 : limit, 1, 50);

        var members = await _clubQuery
            .ListRecentMembersAsync(clubId, sanitizedLimit, ct)
            .ConfigureAwait(false);

        var dtos = members
            .Select(model => model.ToClubMemberDto(currentUserId))
            .ToList();

        return Result<IReadOnlyList<ClubMemberDto>>.Success(dtos);
    }
}
