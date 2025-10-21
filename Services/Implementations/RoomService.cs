using Microsoft.AspNetCore.Identity;
using Services.Common.Mapping;

namespace Services.Implementations;

/// <summary>
/// Room membership service focusing on create/join/kick flows.
/// </summary>
public sealed class RoomService : IRoomService
{
    private readonly IGenericUnitOfWork _uow;
    private readonly IRoomQueryRepository _roomQuery;
    private readonly IRoomCommandRepository _roomCommand;
    private readonly IClubQueryRepository _clubQuery;
    private readonly IPasswordHasher<Room> _passwordHasher;

    public RoomService(
        IGenericUnitOfWork uow,
        IRoomQueryRepository roomQuery,
        IRoomCommandRepository roomCommand,
        IClubQueryRepository clubQuery,
        IPasswordHasher<Room> passwordHasher)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _roomQuery = roomQuery ?? throw new ArgumentNullException(nameof(roomQuery));
        _roomCommand = roomCommand ?? throw new ArgumentNullException(nameof(roomCommand));
        _clubQuery = clubQuery ?? throw new ArgumentNullException(nameof(clubQuery));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    public async Task<Result<RoomDetailDto>> CreateRoomAsync(RoomCreateRequestDto req, Guid currentUserId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Validation, "Room name is required."));
        }

        if (req.JoinPolicy == RoomJoinPolicy.RequiresPassword && string.IsNullOrWhiteSpace(req.Password))
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Validation, "Password is required for password-protected rooms."));
        }

        if (req.Capacity.HasValue && req.Capacity.Value < 1)
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Validation, "Capacity must be at least 1."));
        }

        var club = await _clubQuery.GetByIdAsync(req.ClubId, ct).ConfigureAwait(false);
        if (club is null)
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.NotFound, "Club not found."));
        }

        var clubMembership = await _clubQuery.GetMemberAsync(req.ClubId, currentUserId, ct).ConfigureAwait(false);
        if (clubMembership is null)
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Conflict, "ClubMembershipRequired"));
        }

        var now = DateTime.UtcNow;
        var room = new Room
        {
            Id = Guid.NewGuid(),
            ClubId = req.ClubId,
            Name = req.Name.Trim(),
            Description = NormalizeOrNull(req.Description),
            JoinPolicy = req.JoinPolicy,
            Capacity = req.Capacity,
            MembersCount = 0,
            CreatedAtUtc = now,
            CreatedBy = currentUserId
        };

        if (req.JoinPolicy == RoomJoinPolicy.RequiresPassword && req.Password is not null)
        {
            room.JoinPasswordHash = _passwordHasher.HashPassword(room, req.Password);
        }

        var ownerMember = new RoomMember
        {
            RoomId = room.Id,
            UserId = currentUserId,
            Role = RoomRole.Owner,
            Status = RoomMemberStatus.Approved,
            JoinedAt = now
        };

        var createResult = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            await _roomCommand.CreateRoomAsync(room, innerCt).ConfigureAwait(false);
            await _roomCommand.UpsertMemberAsync(ownerMember, innerCt).ConfigureAwait(false);
            await _roomCommand.IncrementRoomMembersAsync(room.Id, 1, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        if (!createResult.IsSuccess)
        {
            return Result<RoomDetailDto>.Failure(createResult.Error!);
        }

        var detail = await _roomQuery.GetDetailsAsync(room.Id, currentUserId, ct).ConfigureAwait(false);
        if (detail is null)
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Room persisted but could not be reloaded."));
        }

        return Result<RoomDetailDto>.Success(detail.ToRoomDetailDto());
    }

    public async Task<Result<RoomDetailDto>> JoinRoomAsync(Guid roomId, Guid currentUserId, RoomJoinRequestDto? req, CancellationToken ct = default)
    {
        var room = await _roomQuery.GetRoomWithClubCommunityAsync(roomId, ct).ConfigureAwait(false);
        if (room is null)
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.NotFound, "Room not found."));
        }

        var clubMembership = await _clubQuery.GetMemberAsync(room.ClubId, currentUserId, ct).ConfigureAwait(false);
        if (clubMembership is null)
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Conflict, "ClubMembershipRequired"));
        }

        var existingMember = await _roomQuery.GetMemberAsync(roomId, currentUserId, ct).ConfigureAwait(false);
        if (existingMember is not null && existingMember.Status == RoomMemberStatus.Approved)
        {
            var detail = await _roomQuery.GetDetailsAsync(roomId, currentUserId, ct).ConfigureAwait(false);
            if (detail is null)
            {
                return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Unable to load room details."));
            }

            return Result<RoomDetailDto>.Success(detail.ToRoomDetailDto());
        }

        RoomMemberStatus desiredStatus;
        switch (room.JoinPolicy)
        {
            case RoomJoinPolicy.Open:
                if (room.Capacity.HasValue)
                {
                    var approvedCount = await _roomQuery.CountApprovedMembersAsync(roomId, ct).ConfigureAwait(false);
                    if (approvedCount >= room.Capacity.Value)
                    {
                        return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Conflict, "Room is at full capacity."));
                    }
                }
                desiredStatus = RoomMemberStatus.Approved;
                break;
            case RoomJoinPolicy.RequiresApproval:
                desiredStatus = RoomMemberStatus.Pending;
                break;
            case RoomJoinPolicy.RequiresPassword:
                var providedPassword = req?.Password;
                if (string.IsNullOrWhiteSpace(providedPassword))
                {
                    return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Validation, "Password is required."));
                }

                if (string.IsNullOrEmpty(room.JoinPasswordHash))
                {
                    return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Room password is not configured."));
                }

                var verification = _passwordHasher.VerifyHashedPassword(room, room.JoinPasswordHash, providedPassword);
                if (verification == PasswordVerificationResult.Failed)
                {
                    return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Forbidden, "Invalid password."));
                }

                if (room.Capacity.HasValue)
                {
                    var approvedCount = await _roomQuery.CountApprovedMembersAsync(roomId, ct).ConfigureAwait(false);
                    if (approvedCount >= room.Capacity.Value)
                    {
                        return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Conflict, "Room is at full capacity."));
                    }
                }

                desiredStatus = RoomMemberStatus.Approved;
                break;
            default:
                return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Unsupported join policy."));
        }

        var joinResult = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var member = new RoomMember
            {
                RoomId = roomId,
                UserId = currentUserId,
                Role = existingMember?.Role ?? RoomRole.Member,
                Status = desiredStatus,
                JoinedAt = existingMember?.JoinedAt ?? DateTime.UtcNow
            };

            await _roomCommand.UpsertMemberAsync(member, innerCt).ConfigureAwait(false);

            var wasApproved = existingMember?.Status == RoomMemberStatus.Approved;
            if (!wasApproved && desiredStatus == RoomMemberStatus.Approved)
            {
                await _roomCommand.IncrementRoomMembersAsync(roomId, 1, innerCt).ConfigureAwait(false);
            }

            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        if (!joinResult.IsSuccess)
        {
            return Result<RoomDetailDto>.Failure(joinResult.Error!);
        }

        var updatedDetail = await _roomQuery.GetDetailsAsync(roomId, currentUserId, ct).ConfigureAwait(false);
        if (updatedDetail is null)
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Unable to load room details."));
        }

        return Result<RoomDetailDto>.Success(updatedDetail.ToRoomDetailDto());
    }

    public async Task<Result> KickRoomMemberAsync(Guid roomId, Guid targetUserId, Guid actorUserId, CancellationToken ct = default)
    {
        var room = await _roomQuery.GetRoomWithClubCommunityAsync(roomId, ct).ConfigureAwait(false);
        if (room is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Room not found."));
        }

        var actorMembership = await _roomQuery.GetMemberAsync(roomId, actorUserId, ct).ConfigureAwait(false);
        if (actorMembership is null || actorMembership.Status != RoomMemberStatus.Approved || actorMembership.Role != RoomRole.Owner)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Only room owners can remove members."));
        }

        var targetMembership = await _roomQuery.GetMemberAsync(roomId, targetUserId, ct).ConfigureAwait(false);
        if (targetMembership is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Member not found."));
        }

        if (targetMembership.Role == RoomRole.Owner)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Cannot remove a room owner."));
        }

        var kickResult = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            await _roomCommand.RemoveMemberAsync(roomId, targetUserId, innerCt).ConfigureAwait(false);

            if (targetMembership.Status == RoomMemberStatus.Approved)
            {
                await _roomCommand.IncrementRoomMembersAsync(roomId, -1, innerCt).ConfigureAwait(false);
            }

            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        return kickResult;
    }

    public async Task<Result<RoomDetailDto>> GetByIdAsync(Guid roomId, Guid? currentUserId = null, CancellationToken ct = default)
    {
        var detail = await _roomQuery.GetDetailsAsync(roomId, currentUserId, ct).ConfigureAwait(false);
        if (detail is null)
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.NotFound, "Room not found."));
        }

        return Result<RoomDetailDto>.Success(detail.ToRoomDetailDto());
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
