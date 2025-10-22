using Microsoft.EntityFrameworkCore;
using Repositories.Models;
using Repositories.WorkSeeds.Extensions;
using Services.Common.Mapping;

namespace Services.Implementations;

/// <summary>
/// Club membership orchestration.
/// </summary>
public sealed class ClubService : IClubService
{
    private readonly IGenericUnitOfWork _uow;
    private readonly IClubQueryRepository _clubQuery;
    private readonly IClubCommandRepository _clubCommand;
    private readonly ICommunityQueryRepository _communityQuery;
    private readonly IRoomQueryRepository _roomQuery;
    private readonly IRoomCommandRepository _roomCommand;

    public ClubService(
        IGenericUnitOfWork uow,
        IClubQueryRepository clubQuery,
        IClubCommandRepository clubCommand,
        ICommunityQueryRepository communityQuery,
        IRoomQueryRepository roomQuery,
        IRoomCommandRepository roomCommand)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _clubQuery = clubQuery ?? throw new ArgumentNullException(nameof(clubQuery));
        _clubCommand = clubCommand ?? throw new ArgumentNullException(nameof(clubCommand));
        _communityQuery = communityQuery ?? throw new ArgumentNullException(nameof(communityQuery));
        _roomQuery = roomQuery ?? throw new ArgumentNullException(nameof(roomQuery));
        _roomCommand = roomCommand ?? throw new ArgumentNullException(nameof(roomCommand));
    }

    public async Task<Result<CursorPageResult<ClubBriefDto>>> SearchAsync(
        Guid communityId,
        string? name,
        bool? isPublic,
        int? membersFrom,
        int? membersTo,
        CursorRequest cursor,
        CancellationToken ct = default)
    {
        if (communityId == Guid.Empty)
        {
            return Result<CursorPageResult<ClubBriefDto>>.Failure(
                new Error(Error.Codes.Validation, "CommunityId is required."));
        }

        if (membersFrom.HasValue && membersFrom.Value < 0)
        {
            return Result<CursorPageResult<ClubBriefDto>>.Failure(
                new Error(Error.Codes.Validation, "membersFrom must be non-negative."));
        }

        if (membersTo.HasValue && membersTo.Value < 0)
        {
            return Result<CursorPageResult<ClubBriefDto>>.Failure(
                new Error(Error.Codes.Validation, "membersTo must be non-negative."));
        }

        if (membersFrom.HasValue && membersTo.HasValue && membersFrom.Value > membersTo.Value)
        {
            return Result<CursorPageResult<ClubBriefDto>>.Failure(
                new Error(Error.Codes.Validation, "membersFrom cannot be greater than membersTo."));
        }

        var (items, nextCursor) = await _clubQuery.SearchClubsAsync(
            communityId,
            name,
            isPublic,
            membersFrom,
            membersTo,
            cursor,
            ct).ConfigureAwait(false);

        var dtos = items.Select(c => c.ToClubBriefDto()).ToList();

        var page = new CursorPageResult<ClubBriefDto>(
            Items: dtos,
            NextCursor: nextCursor,
            PrevCursor: null,
            Size: cursor.SizeSafe,
            Sort: cursor.SortSafe,
            Desc: cursor.Desc);

        return Result<CursorPageResult<ClubBriefDto>>.Success(page);
    }

    public async Task<Result<ClubDetailDto>> CreateClubAsync(ClubCreateRequestDto req, Guid currentUserId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return Result<ClubDetailDto>.Failure(new Error(Error.Codes.Validation, "Club name is required."));
        }

        var name = req.Name.Trim();
        if (name.Length > 200)
        {
            return Result<ClubDetailDto>.Failure(new Error(Error.Codes.Validation, "Club name must be at most 200 characters."));
        }

        var communityMembership = await _communityQuery.GetMemberAsync(req.CommunityId, currentUserId, ct).ConfigureAwait(false);
        if (communityMembership is null)
        {
            return Result<ClubDetailDto>.Failure(new Error(Error.Codes.Conflict, "CommunityMembershipRequired"));
        }

        var now = DateTime.UtcNow;
        var club = new Club
        {
            Id = Guid.NewGuid(),
            CommunityId = req.CommunityId,
            Name = name,
            Description = NormalizeOrNull(req.Description),
            IsPublic = req.IsPublic,
            MembersCount = 1,
            CreatedAtUtc = now,
            CreatedBy = currentUserId
        };

        var ownerMember = new ClubMember
        {
            ClubId = club.Id,
            UserId = currentUserId,
            Role = MemberRole.Owner,
            JoinedAt = now
        };

        var transaction = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            await _clubCommand.CreateAsync(club, innerCt).ConfigureAwait(false);
            await _clubCommand.AddMemberAsync(ownerMember, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        if (!transaction.IsSuccess)
        {
            return Result<ClubDetailDto>.Failure(transaction.Error!);
        }

        var detailModel = await _clubQuery.GetDetailsAsync(club.Id, currentUserId, ct).ConfigureAwait(false);
        if (detailModel is null)
        {
            return Result<ClubDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Club persisted but could not be reloaded."));
        }

        return Result<ClubDetailDto>.Success(detailModel.ToClubDetailDto());
    }

    public async Task<Result<ClubDetailDto>> UpdateClubAsync(Guid id, ClubUpdateRequestDto req, Guid actorId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (id == Guid.Empty)
        {
            return Result<ClubDetailDto>.Failure(new Error(Error.Codes.Validation, "ClubId is required."));
        }

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return Result<ClubDetailDto>.Failure(new Error(Error.Codes.Validation, "Club name is required."));
        }

        var name = req.Name.Trim();
        if (name.Length > 200)
        {
            return Result<ClubDetailDto>.Failure(new Error(Error.Codes.Validation, "Club name must be at most 200 characters."));
        }

        var club = await _clubQuery.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (club is null)
        {
            return Result<ClubDetailDto>.Failure(new Error(Error.Codes.NotFound, "Club not found."));
        }

        var membership = await _clubQuery.GetMemberAsync(id, actorId, ct).ConfigureAwait(false);
        if (membership is null || membership.Role != MemberRole.Owner)
        {
            return Result<ClubDetailDto>.Failure(new Error(Error.Codes.Forbidden, "Only club owners can update the club."));
        }

        club.Name = name;
        club.Description = NormalizeOrNull(req.Description);
        club.IsPublic = req.IsPublic;
        club.UpdatedAtUtc = DateTime.UtcNow;
        club.UpdatedBy = actorId;

        var updateResult = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            await _clubCommand.UpdateAsync(club, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        if (!updateResult.IsSuccess)
        {
            return Result<ClubDetailDto>.Failure(updateResult.Error!);
        }

        var detail = await _clubQuery.GetDetailsAsync(id, actorId, ct).ConfigureAwait(false);
        if (detail is null)
        {
            return Result<ClubDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Unable to load club details."));
        }

        return Result<ClubDetailDto>.Success(detail.ToClubDetailDto());
    }

    public async Task<Result<ClubDetailDto>> JoinClubAsync(Guid clubId, Guid currentUserId, CancellationToken ct = default)
    {
        var club = await _clubQuery.GetByIdAsync(clubId, ct).ConfigureAwait(false);
        if (club is null)
        {
            return Result<ClubDetailDto>.Failure(new Error(Error.Codes.NotFound, "Club not found."));
        }

        var existingMembership = await _clubQuery.GetMemberAsync(clubId, currentUserId, ct).ConfigureAwait(false);
        if (existingMembership is not null)
        {
            var existingDetail = await _clubQuery.GetDetailsAsync(clubId, currentUserId, ct).ConfigureAwait(false);
            if (existingDetail is null)
            {
                return Result<ClubDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Unable to load club details."));
            }

            return Result<ClubDetailDto>.Success(existingDetail.ToClubDetailDto());
        }

        var joinResult = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var member = new ClubMember
            {
                ClubId = clubId,
                UserId = currentUserId,
                Role = MemberRole.Member,
                JoinedAt = DateTime.UtcNow
            };

            await _clubCommand.AddMemberAsync(member, innerCt).ConfigureAwait(false);

            try
            {
                await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
            {
                _clubCommand.Detach(member);
                return Result<bool>.Success(false);
            }

            await _roomCommand.IncrementClubMembersAsync(clubId, 1, innerCt).ConfigureAwait(false);
            return Result<bool>.Success(true);
        }, ct: ct).ConfigureAwait(false);

        if (!joinResult.IsSuccess)
        {
            return Result<ClubDetailDto>.Failure(joinResult.Error!);
        }

        var detail = await _clubQuery.GetDetailsAsync(clubId, currentUserId, ct).ConfigureAwait(false);
        if (detail is null)
        {
            return Result<ClubDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Unable to load club details."));
        }

        return Result<ClubDetailDto>.Success(detail.ToClubDetailDto());
    }

    public async Task<Result> DeleteClubAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "ClubId is required."));
        }

        var club = await _clubQuery.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (club is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Club not found."));
        }

        var membership = await _clubQuery.GetMemberAsync(id, actorId, ct).ConfigureAwait(false);
        if (membership is null || membership.Role != MemberRole.Owner)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Only club owners can delete the club."));
        }

        // Force explicit room cleanup before deleting the parent club.
        var hasRooms = await _roomQuery.AnyByClubAsync(id, ct).ConfigureAwait(false);
        if (hasRooms)
        {
            return Result.Failure(new Error(Error.Codes.Conflict, "ClubHasRooms"));
        }

        return await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            await _clubCommand.SoftDeleteAsync(id, actorId, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);
    }

    public async Task<Result> KickClubMemberAsync(Guid clubId, Guid targetUserId, Guid actorUserId, CancellationToken ct = default)
    {
        var club = await _clubQuery.GetByIdAsync(clubId, ct).ConfigureAwait(false);
        if (club is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Club not found."));
        }

        var actorMembership = await _clubQuery.GetMemberAsync(clubId, actorUserId, ct).ConfigureAwait(false);
        if (actorMembership is null || actorMembership.Role != MemberRole.Owner)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Only club owners can remove members."));
        }

        var targetMembership = await _clubQuery.GetMemberAsync(clubId, targetUserId, ct).ConfigureAwait(false);
        if (targetMembership is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Member not found."));
        }

        if (targetMembership.Role == MemberRole.Owner)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Cannot remove a club owner."));
        }

        var kickResult = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var roomSummaries = await _roomCommand.RemoveMembershipsByClubAsync(clubId, targetUserId, innerCt).ConfigureAwait(false);

            foreach (var summary in roomSummaries)
            {
                if (summary.ApprovedRemovedCount > 0)
                {
                    await _roomCommand.IncrementRoomMembersAsync(summary.RoomId, -summary.ApprovedRemovedCount, innerCt).ConfigureAwait(false);
                }
            }

            await _clubCommand.RemoveMemberAsync(clubId, targetUserId, innerCt).ConfigureAwait(false);
            await _roomCommand.IncrementClubMembersAsync(clubId, -1, innerCt).ConfigureAwait(false);

            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        return kickResult;
    }

    public async Task<Result<ClubDetailDto>> GetByIdAsync(Guid clubId, Guid? currentUserId = null, CancellationToken ct = default)
    {
        var detail = await _clubQuery.GetDetailsAsync(clubId, currentUserId, ct).ConfigureAwait(false);
        if (detail is null)
        {
            return Result<ClubDetailDto>.Failure(new Error(Error.Codes.NotFound, "Club not found."));
        }

        return Result<ClubDetailDto>.Success(detail.ToClubDetailDto());
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
