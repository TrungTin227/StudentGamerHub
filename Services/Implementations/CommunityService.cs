namespace Services.Implementations;

/// <summary>
/// Community service implementation.
/// </summary>
public sealed class CommunityService : ICommunityService
{
    private readonly IGenericUnitOfWork _uow;
    private readonly ICommunityQueryRepository _communityQuery;
    private readonly ICommunityCommandRepository _communityCommand;

    public CommunityService(
        IGenericUnitOfWork uow,
        ICommunityQueryRepository communityQuery,
        ICommunityCommandRepository communityCommand)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _communityQuery = communityQuery ?? throw new ArgumentNullException(nameof(communityQuery));
        _communityCommand = communityCommand ?? throw new ArgumentNullException(nameof(communityCommand));
    }

    public async Task<Result<Guid>> CreateAsync(Guid currentUserId, CommunityCreateRequestDto req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (string.IsNullOrWhiteSpace(req.Name))
            return Result<Guid>.Failure(new Error(Error.Codes.Validation, "Community name is required."));

        var community = new Community
        {
            Id = Guid.NewGuid(),
            Name = req.Name.Trim(),
            Description = NormalizeOrNull(req.Description),
            School = NormalizeOrNull(req.School),
            IsPublic = req.IsPublic,
            MembersCount = 0,
            CreatedBy = currentUserId,
        };

        return await _uow.ExecuteTransactionAsync<Guid>(async innerCt =>
        {
            await _communityCommand.CreateAsync(community, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result<Guid>.Success(community.Id);
        }, ct: ct).ConfigureAwait(false);
    }

    public async Task<Result<CommunityDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var community = await _communityQuery.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (community is null)
            return Result<CommunityDetailDto>.Failure(new Error(Error.Codes.NotFound, "Community not found."));

        return Result<CommunityDetailDto>.Success(community.ToDetailDto());
    }

    public async Task<Result> UpdateAsync(Guid currentUserId, Guid id, CommunityUpdateRequestDto req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (string.IsNullOrWhiteSpace(req.Name))
            return Result.Failure(new Error(Error.Codes.Validation, "Community name is required."));

        return await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var community = await _communityQuery.GetByIdAsync(id, innerCt).ConfigureAwait(false);
            if (community is null)
                return Result.Failure(new Error(Error.Codes.NotFound, "Community not found."));

            community.Name = req.Name.Trim();
            community.Description = NormalizeOrNull(req.Description);
            community.School = NormalizeOrNull(req.School);
            community.IsPublic = req.IsPublic;
            community.UpdatedBy = currentUserId;

            if (community.MembersCount < 0)
                community.MembersCount = 0;

            await _communityCommand.UpdateAsync(community, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);
    }

    public async Task<Result> ArchiveAsync(Guid currentUserId, Guid id, CancellationToken ct = default)
    {
        return await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var community = await _communityQuery.GetByIdAsync(id, innerCt).ConfigureAwait(false);
            if (community is null)
                return Result.Failure(new Error(Error.Codes.NotFound, "Community not found."));

            var hasApprovedRooms = await _communityQuery.HasAnyApprovedRoomsAsync(id, innerCt).ConfigureAwait(false);
            if (hasApprovedRooms)
                return Result.Failure(new Error(Error.Codes.Forbidden, "Community still has approved room members."));

            await _communityCommand.SoftDeleteAsync(id, currentUserId, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);
    }

    private static string? NormalizeOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }
}
