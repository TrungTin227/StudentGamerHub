namespace Services.Implementations;

/// <summary>
/// Club service implementation.
/// Manages club search, creation, and retrieval within communities.
/// All write operations are wrapped in transactions via IGenericUnitOfWork.
/// </summary>
public sealed class ClubService : IClubService
{
    private readonly IGenericUnitOfWork _uow;
    private readonly IClubQueryRepository _clubQuery;
    private readonly IClubCommandRepository _clubCommand;

    public ClubService(
        IGenericUnitOfWork uow,
        IClubQueryRepository clubQuery,
        IClubCommandRepository clubCommand)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _clubQuery = clubQuery ?? throw new ArgumentNullException(nameof(clubQuery));
        _clubCommand = clubCommand ?? throw new ArgumentNullException(nameof(clubCommand));
    }

    /// <inheritdoc/>
    public async Task<Result<CursorPageResult<ClubBriefDto>>> SearchAsync(
        Guid communityId,
        string? name,
        bool? isPublic,
        int? membersFrom,
        int? membersTo,
        CursorRequest cursor,
        CancellationToken ct = default)
    {
        // Validation: member count range
        if (membersFrom.HasValue && membersFrom.Value < 0)
            return Result<CursorPageResult<ClubBriefDto>>.Failure(
                new Error(Error.Codes.Validation, "membersFrom must be >= 0."));

        if (membersTo.HasValue && membersTo.Value < 0)
            return Result<CursorPageResult<ClubBriefDto>>.Failure(
                new Error(Error.Codes.Validation, "membersTo must be >= 0."));

        if (membersFrom.HasValue && membersTo.HasValue && membersFrom.Value > membersTo.Value)
            return Result<CursorPageResult<ClubBriefDto>>.Failure(
                new Error(Error.Codes.Validation, "membersFrom must be <= membersTo."));

        // Query clubs
        var (clubs, nextCursor) = await _clubQuery.SearchClubsAsync(
            communityId,
            name,
            isPublic,
            membersFrom,
            membersTo,
            cursor,
            ct);

        // Map to DTOs
        var dtos = clubs.Select(c => c.ToClubBriefDto()).ToList();

        var result = new CursorPageResult<ClubBriefDto>(
            Items: dtos,
            NextCursor: nextCursor,
            PrevCursor: null, // Simple implementation: only support forward pagination
            Size: cursor.SizeSafe,
            Sort: cursor.SortSafe,
            Desc: cursor.Desc
        );

        return Result<CursorPageResult<ClubBriefDto>>.Success(result);
    }

    /// <inheritdoc/>
    public async Task<Result<Guid>> CreateClubAsync(
        Guid currentUserId,
        Guid communityId,
        string name,
        string? description,
        bool isPublic,
        CancellationToken ct = default)
    {
        // Validation: name required
        if (string.IsNullOrWhiteSpace(name))
            return Result<Guid>.Failure(
                new Error(Error.Codes.Validation, "Club name is required."));

        // Trim inputs
        name = name.Trim();
        description = NormalizeOrNull(description);

        // Validate name length (reasonable limit)
        if (name.Length > 256)
            return Result<Guid>.Failure(
                new Error(Error.Codes.Validation, "Club name must not exceed 256 characters."));

        // Transaction: create club
        return await _uow.ExecuteTransactionAsync<Guid>(async ctk =>
        {
            // Create club entity
            var club = new Club
            {
                Id = Guid.NewGuid(),
                CommunityId = communityId,
                Name = name,
                Description = description,
                IsPublic = isPublic,
                MembersCount = 0,
                CreatedBy = currentUserId
            };

            // Note: Audit fields (CreatedBy, CreatedAtUtc) are auto-set by AppDbContext.SaveChanges

            await _clubCommand.CreateAsync(club, ctk);
            await _uow.SaveChangesAsync(ctk);

            return Result<Guid>.Success(club.Id);
        }, ct: ct);
    }

    /// <inheritdoc/>
    public async Task<Result<ClubDetailDto>> GetByIdAsync(Guid clubId, CancellationToken ct = default)
    {
        var club = await _clubQuery.GetByIdAsync(clubId, ct);

        if (club is null)
            return Result<ClubDetailDto>.Failure(
                new Error(Error.Codes.NotFound, $"Club with ID '{clubId}' not found."));

        var dto = club.ToClubDetailDto();
        return Result<ClubDetailDto>.Success(dto);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(Guid currentUserId, Guid id, ClubUpdateRequestDto req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (string.IsNullOrWhiteSpace(req.Name))
            return Result.Failure(new Error(Error.Codes.Validation, "Club name is required."));

        var name = req.Name.Trim();
        if (name.Length > 256)
            return Result.Failure(new Error(Error.Codes.Validation, "Club name must not exceed 256 characters."));

        return await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var club = await _clubQuery.GetByIdAsync(id, innerCt).ConfigureAwait(false);
            if (club is null)
                return Result.Failure(new Error(Error.Codes.NotFound, "Club not found."));

            if (!IsAuthorized(currentUserId, club))
                return Result.Failure(new Error(Error.Codes.Forbidden, "You are not allowed to update this club."));

            club.Name = name;
            club.Description = NormalizeOrNull(req.Description);
            club.IsPublic = req.IsPublic;
            club.UpdatedBy = currentUserId;
            club.UpdatedAtUtc = DateTime.UtcNow;

            await _clubCommand.UpdateAsync(club, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Result> ArchiveAsync(Guid currentUserId, Guid id, CancellationToken ct = default)
    {
        return await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var club = await _clubQuery.GetByIdAsync(id, innerCt).ConfigureAwait(false);
            if (club is null)
                return Result.Failure(new Error(Error.Codes.NotFound, "Club not found."));

            if (!IsAuthorized(currentUserId, club))
                return Result.Failure(new Error(Error.Codes.Forbidden, "You are not allowed to archive this club."));

            var hasApprovedRooms = await _clubQuery.HasAnyApprovedRoomsAsync(id, innerCt).ConfigureAwait(false);
            if (hasApprovedRooms)
                return Result.Failure(new Error(Error.Codes.Forbidden, "Club still has approved room members."));

            await _clubCommand.SoftDeleteAsync(id, currentUserId, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);
    }

    private static bool IsAuthorized(Guid currentUserId, Club club)
    {
        return club.CreatedBy == currentUserId;
    }

    private static string? NormalizeOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }
}
