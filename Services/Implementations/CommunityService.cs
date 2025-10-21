using Services.Common.Mapping;

namespace Services.Implementations;

/// <summary>
/// Community membership orchestration service.
/// </summary>
public sealed class CommunityService : ICommunityService
{
    private readonly IGenericUnitOfWork _uow;
    private readonly ICommunityQueryRepository _communityQuery;
    private readonly ICommunityCommandRepository _communityCommand;
    private readonly IClubQueryRepository _clubQuery;
    private readonly IClubCommandRepository _clubCommand;
    private readonly IRoomQueryRepository _roomQuery;
    private readonly IRoomCommandRepository _roomCommand;

    public CommunityService(
        IGenericUnitOfWork uow,
        ICommunityQueryRepository communityQuery,
        ICommunityCommandRepository communityCommand,
        IClubQueryRepository clubQuery,
        IClubCommandRepository clubCommand,
        IRoomQueryRepository roomQuery,
        IRoomCommandRepository roomCommand)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _communityQuery = communityQuery ?? throw new ArgumentNullException(nameof(communityQuery));
        _communityCommand = communityCommand ?? throw new ArgumentNullException(nameof(communityCommand));
        _clubQuery = clubQuery ?? throw new ArgumentNullException(nameof(clubQuery));
        _clubCommand = clubCommand ?? throw new ArgumentNullException(nameof(clubCommand));
        _roomQuery = roomQuery ?? throw new ArgumentNullException(nameof(roomQuery));
        _roomCommand = roomCommand ?? throw new ArgumentNullException(nameof(roomCommand));
    }

    public async Task<Result<CommunityDetailDto>> CreateCommunityAsync(CommunityCreateRequestDto req, Guid currentUserId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return Result<CommunityDetailDto>.Failure(new Error(Error.Codes.Validation, "Community name is required."));
        }

        var normalizedName = req.Name.Trim();
        if (normalizedName.Length > 200)
        {
            return Result<CommunityDetailDto>.Failure(new Error(Error.Codes.Validation, "Community name must be at most 200 characters."));
        }

        var now = DateTime.UtcNow;
        var community = new Community
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Description = NormalizeOrNull(req.Description),
            School = NormalizeOrNull(req.School),
            IsPublic = req.IsPublic,
            MembersCount = 1,
            CreatedAtUtc = now,
            CreatedBy = currentUserId
        };

        var ownerMember = new CommunityMember
        {
            CommunityId = community.Id,
            UserId = currentUserId,
            Role = MemberRole.Owner,
            JoinedAt = now
        };

        var transactionResult = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            await _communityCommand.CreateAsync(community, innerCt).ConfigureAwait(false);
            await _communityCommand.AddMemberAsync(ownerMember, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        if (!transactionResult.IsSuccess)
        {
            return Result<CommunityDetailDto>.Failure(transactionResult.Error!);
        }

        var detailModel = await _communityQuery.GetDetailsAsync(community.Id, currentUserId, ct).ConfigureAwait(false);
        if (detailModel is null)
        {
            return Result<CommunityDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Community persisted but could not be reloaded."));
        }

        return Result<CommunityDetailDto>.Success(detailModel.ToDetailDto());
    }

    public async Task<Result<CommunityDetailDto>> JoinCommunityAsync(Guid communityId, Guid currentUserId, CancellationToken ct = default)
    {
        var community = await _communityQuery.GetByIdAsync(communityId, ct).ConfigureAwait(false);
        if (community is null)
        {
            return Result<CommunityDetailDto>.Failure(new Error(Error.Codes.NotFound, "Community not found."));
        }

        var existingMembership = await _communityQuery.GetMemberAsync(communityId, currentUserId, ct).ConfigureAwait(false);
        if (existingMembership is not null)
        {
            var existingDetail = await _communityQuery.GetDetailsAsync(communityId, currentUserId, ct).ConfigureAwait(false);
            if (existingDetail is null)
            {
                return Result<CommunityDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Unable to load community details."));
            }

            return Result<CommunityDetailDto>.Success(existingDetail.ToDetailDto());
        }

        var joinResult = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var member = new CommunityMember
            {
                CommunityId = communityId,
                UserId = currentUserId,
                Role = MemberRole.Member,
                JoinedAt = DateTime.UtcNow
            };

            await _communityCommand.AddMemberAsync(member, innerCt).ConfigureAwait(false);
            await _roomCommand.IncrementCommunityMembersAsync(communityId, 1, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        if (!joinResult.IsSuccess)
        {
            return Result<CommunityDetailDto>.Failure(joinResult.Error!);
        }

        var detail = await _communityQuery.GetDetailsAsync(communityId, currentUserId, ct).ConfigureAwait(false);
        if (detail is null)
        {
            return Result<CommunityDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Unable to load community details."));
        }

        return Result<CommunityDetailDto>.Success(detail.ToDetailDto());
    }

    public async Task<Result> KickCommunityMemberAsync(Guid communityId, Guid targetUserId, Guid actorUserId, CancellationToken ct = default)
    {
        var community = await _communityQuery.GetByIdAsync(communityId, ct).ConfigureAwait(false);
        if (community is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Community not found."));
        }

        var actorMembership = await _communityQuery.GetMemberAsync(communityId, actorUserId, ct).ConfigureAwait(false);
        if (actorMembership is null || actorMembership.Role != MemberRole.Owner)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Only community owners can remove members."));
        }

        var targetMembership = await _communityQuery.GetMemberAsync(communityId, targetUserId, ct).ConfigureAwait(false);
        if (targetMembership is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Member not found."));
        }

        if (targetMembership.Role == MemberRole.Owner)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Cannot remove a community owner."));
        }

        var clubMemberships = await _clubQuery.ListMembershipsAsync(communityId, targetUserId, ct).ConfigureAwait(false);

        var kickResult = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            await _communityCommand.RemoveMemberAsync(communityId, targetUserId, innerCt).ConfigureAwait(false);
            await _roomCommand.IncrementCommunityMembersAsync(communityId, -1, innerCt).ConfigureAwait(false);

            foreach (var clubMember in clubMemberships)
            {
                await _clubCommand.RemoveMemberAsync(clubMember.ClubId, targetUserId, innerCt).ConfigureAwait(false);
                await _roomCommand.IncrementClubMembersAsync(clubMember.ClubId, -1, innerCt).ConfigureAwait(false);

                var roomMemberships = await _roomQuery.ListMembershipsAsync(clubMember.ClubId, targetUserId, innerCt).ConfigureAwait(false);

                foreach (var roomMember in roomMemberships)
                {
                    await _roomCommand.RemoveMemberAsync(roomMember.RoomId, targetUserId, innerCt).ConfigureAwait(false);

                    if (roomMember.Status == RoomMemberStatus.Approved)
                    {
                        await _roomCommand.IncrementRoomMembersAsync(roomMember.RoomId, -1, innerCt).ConfigureAwait(false);
                    }
                }
            }

            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        return kickResult;
    }

    public async Task<Result<CommunityDetailDto>> GetByIdAsync(Guid communityId, Guid? currentUserId = null, CancellationToken ct = default)
    {
        var detail = await _communityQuery.GetDetailsAsync(communityId, currentUserId, ct).ConfigureAwait(false);
        if (detail is null)
        {
            return Result<CommunityDetailDto>.Failure(new Error(Error.Codes.NotFound, "Community not found."));
        }

        return Result<CommunityDetailDto>.Success(detail.ToDetailDto());
    }

    private static string? NormalizeOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
