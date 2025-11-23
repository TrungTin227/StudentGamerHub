using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Repositories.WorkSeeds.Extensions;
using Services.Common.Mapping;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly IClubCommandRepository _clubCommand;
    private readonly ICommunityQueryRepository _communityQuery;
    private readonly ICommunityCommandRepository _communityCommand;
    private readonly IPasswordHasher<Room> _passwordHasher;
    private readonly ILogger<RoomService> _logger;

    public RoomService(
        IGenericUnitOfWork uow,
        IRoomQueryRepository roomQuery,
        IRoomCommandRepository roomCommand,
        IClubQueryRepository clubQuery,
        IClubCommandRepository clubCommand,
        ICommunityQueryRepository communityQuery,
        ICommunityCommandRepository communityCommand,
        IPasswordHasher<Room> passwordHasher,
        ILogger<RoomService>? logger = null)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _roomQuery = roomQuery ?? throw new ArgumentNullException(nameof(roomQuery));
        _roomCommand = roomCommand ?? throw new ArgumentNullException(nameof(roomCommand));
        _clubQuery = clubQuery ?? throw new ArgumentNullException(nameof(clubQuery));
        _clubCommand = clubCommand ?? throw new ArgumentNullException(nameof(clubCommand));
        _communityQuery = communityQuery ?? throw new ArgumentNullException(nameof(communityQuery));
        _communityCommand = communityCommand ?? throw new ArgumentNullException(nameof(communityCommand));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _logger = logger ?? NullLogger<RoomService>.Instance;
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

    public async Task<Result<RoomDetailDto>> UpdateRoomAsync(Guid id, RoomUpdateRequestDto req, Guid actorId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (id == Guid.Empty)
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Validation, "RoomId is required."));
        }

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Validation, "Room name is required."));
        }

        var name = req.Name.Trim();
        if (name.Length > 200)
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Validation, "Room name must be at most 200 characters."));
        }

        var room = await _roomQuery.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (room is null)
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.NotFound, "Room not found."));
        }

        var membership = await _roomQuery.GetMemberAsync(id, actorId, ct).ConfigureAwait(false);
        if (membership is null || membership.Status != RoomMemberStatus.Approved || membership.Role != RoomRole.Owner)
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Forbidden, "Only room owners can update the room."));
        }

        if (req.Capacity.HasValue)
        {
            if (req.Capacity.Value < 1)
            {
                return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Validation, "Capacity must be at least 1."));
            }

            if (req.Capacity.Value < room.MembersCount)
            {
                return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Validation, "Capacity cannot be less than current approved members."));
            }
        }

        room.Name = name;
        room.Description = NormalizeOrNull(req.Description);
        room.JoinPolicy = req.JoinPolicy;
        room.Capacity = req.Capacity;
        room.UpdatedAtUtc = DateTime.UtcNow;
        room.UpdatedBy = actorId;

        if (req.JoinPolicy == RoomJoinPolicy.RequiresPassword)
        {
            if (string.IsNullOrWhiteSpace(req.Password))
            {
                if (string.IsNullOrEmpty(room.JoinPasswordHash))
                {
                    return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Validation, "Password is required for password-protected rooms."));
                }
            }
            else
            {
                room.JoinPasswordHash = _passwordHasher.HashPassword(room, req.Password);
            }
        }
        else
        {
            room.JoinPasswordHash = null;
        }

        var updateResult = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            await _roomCommand.UpdateRoomAsync(room, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        if (!updateResult.IsSuccess)
        {
            return Result<RoomDetailDto>.Failure(updateResult.Error!);
        }

        var detail = await _roomQuery.GetDetailsAsync(id, actorId, ct).ConfigureAwait(false);
        if (detail is null)
        {
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Unable to load room details."));
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

        // ✅ NEW: Tự động join Club nếu chưa join
        var clubMembership = await _clubQuery.GetMemberAsync(room.ClubId, currentUserId, ct).ConfigureAwait(false);
        var autoJoinedClub = false;

        if (clubMembership is null)
        {
            // Kiểm tra xem có cần join Community không (nếu community là private)
            var community = room.Club?.Community;
            if (community is null)
            {
                // Load community nếu chưa có
                community = await _communityQuery.GetByIdAsync(room.Club!.CommunityId, ct).ConfigureAwait(false);
                if (community is null)
                {
                    return Result<RoomDetailDto>.Failure(new Error(Error.Codes.NotFound, "Community not found."));
                }
            }

            // Nếu community là private, kiểm tra xem user đã join community chưa
            if (!community.IsPublic)
            {
                var communityMembership = await _communityQuery
                    .GetMemberAsync(community.Id, currentUserId, ct)
                    .ConfigureAwait(false);

                if (communityMembership is null)
                {
                    return Result<RoomDetailDto>.Failure(
                        new Error(Error.Codes.Forbidden, "You must join the community before joining this club. Community membership is required for private communities."));
                }
            }

            // ✅ Tự động join vào Club
            _logger.LogInformation(
                "User {UserId} is auto-joining club {ClubId} to access room {RoomId}.",
                currentUserId,
                room.ClubId,
                roomId);

            var now = DateTime.UtcNow;
            var newClubMember = new ClubMember
            {
                ClubId = room.ClubId,
                UserId = currentUserId,
                Role = MemberRole.Member,
                JoinedAt = now
            };

            try
            {
                await _clubCommand.AddMemberAsync(newClubMember, ct).ConfigureAwait(false);
                await _uow.SaveChangesAsync(ct).ConfigureAwait(false);
                await _roomCommand.IncrementClubMembersAsync(room.ClubId, 1, ct).ConfigureAwait(false);
                autoJoinedClub = true;

                _logger.LogInformation(
                    "User {UserId} successfully auto-joined club {ClubId}.",
                    currentUserId,
                    room.ClubId);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
            {
                // Concurrent insert - user đã join, reload membership
                _clubCommand.Detach(newClubMember);
                clubMembership = await _clubQuery.GetMemberAsync(room.ClubId, currentUserId, ct).ConfigureAwait(false);
                
                if (clubMembership is null)
                {
                    return Result<RoomDetailDto>.Failure(
                        new Error(Error.Codes.Unexpected, "Failed to verify club membership after concurrent join."));
                }
            }
        }

        // Tiếp tục logic join room như cũ
        var existingMember = await _roomQuery.GetMemberAsync(roomId, currentUserId, ct).ConfigureAwait(false);
        if (existingMember is not null && existingMember.Status == RoomMemberStatus.Approved)
        {
            var existingDetail = await _roomQuery.GetDetailsAsync(roomId, currentUserId, ct).ConfigureAwait(false);
            if (existingDetail is null)
            {
                return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Unable to load room details."));
            }

            if (autoJoinedClub)
            {
                _logger.LogInformation(
                    "User {UserId} was already a member of room {RoomId} (auto-joined club {ClubId}).",
                    currentUserId,
                    roomId,
                    room.ClubId);
            }

            return Result<RoomDetailDto>.Success(existingDetail.ToRoomDetailDto());
        }

        var desiredRole = existingMember?.Role ?? RoomRole.Member;

        RoomMemberStatus desiredStatus;
        switch (room.JoinPolicy)
        {
            case RoomJoinPolicy.Open:
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
                desiredStatus = RoomMemberStatus.Approved;
                break;
            default:
                return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Unsupported join policy."));
        }

        if (existingMember is not null && existingMember.Status == desiredStatus)
        {
            var detail0 = await _roomQuery.GetDetailsAsync(roomId, currentUserId, ct).ConfigureAwait(false);
            if (detail0 is null)
            {
                return Result<RoomDetailDto>.Failure(new Error(Error.Codes.Unexpected, "Unable to load room details."));
            }

            return Result<RoomDetailDto>.Success(detail0.ToRoomDetailDto());
        }

        var joinResult = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;
            var currentStatus = await _roomCommand.GetMemberStatusAsync(roomId, currentUserId, innerCt).ConfigureAwait(false);

            if (currentStatus is null)
            {
                if (desiredStatus == RoomMemberStatus.Approved && room.Capacity.HasValue)
                {
                    var approvedCount = await _roomCommand.CountApprovedMembersAsync(roomId, innerCt).ConfigureAwait(false);
                    if (approvedCount >= room.Capacity.Value)
                    {
                        return Result<RoomMemberStatus>.Failure(new Error(Error.Codes.Conflict, "RoomCapacityExceeded"));
                    }
                }

                var newMember = new RoomMember
                {
                    RoomId = roomId,
                    UserId = currentUserId,
                    Role = desiredRole,
                    Status = desiredStatus,
                    JoinedAt = now
                };

                await _roomCommand.UpsertMemberAsync(newMember, innerCt).ConfigureAwait(false);

                var saved = false;
                try
                {
                    await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
                    saved = true;
                }
                catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
                {
                    _roomCommand.Detach(newMember);

                    // Another request inserted concurrently → treat as idempotent: reload status
                    currentStatus = await _roomCommand.GetMemberStatusAsync(roomId, currentUserId, innerCt).ConfigureAwait(false);
                    if (currentStatus is null)
                    {
                        return Result<RoomMemberStatus>.Failure(new Error(Error.Codes.Unexpected, "Membership reload failed."));
                    }
                }

                if (saved)
                {
                    if (desiredStatus == RoomMemberStatus.Approved)
                    {
                        await _roomCommand.IncrementRoomMembersAsync(roomId, 1, innerCt).ConfigureAwait(false);
                    }

                    if (autoJoinedClub)
                    {
                        _logger.LogInformation(
                            "User {UserId} successfully joined room {RoomId} (auto-joined club {ClubId} first).",
                            currentUserId,
                            roomId,
                            room.ClubId);
                    }

                    return Result<RoomMemberStatus>.Success(desiredStatus);
                }
            }

            if (currentStatus is null)
            {
                return Result<RoomMemberStatus>.Failure(new Error(Error.Codes.Unexpected, "Membership state missing."));
            }

            var finalStatus = currentStatus.Value;

            if (desiredStatus != finalStatus)
            {
                if (desiredStatus == RoomMemberStatus.Approved)
                {
                    if (room.Capacity.HasValue)
                    {
                        var approvedCount = await _roomCommand.CountApprovedMembersAsync(roomId, innerCt).ConfigureAwait(false);
                        if (approvedCount >= room.Capacity.Value)
                        {
                            return Result<RoomMemberStatus>.Failure(new Error(Error.Codes.Conflict, "RoomCapacityExceeded"));
                        }
                    }

                    await _roomCommand.UpdateMemberStatusAsync(roomId, currentUserId, RoomMemberStatus.Approved, now, currentUserId, innerCt).ConfigureAwait(false);
                    await _roomCommand.IncrementRoomMembersAsync(roomId, 1, innerCt).ConfigureAwait(false);
                    finalStatus = RoomMemberStatus.Approved;
                }
                else
                {
                    await _roomCommand.UpdateMemberStatusAsync(roomId, currentUserId, desiredStatus, now, currentUserId, innerCt).ConfigureAwait(false);

                    if (finalStatus == RoomMemberStatus.Approved && desiredStatus != RoomMemberStatus.Approved)
                    {
                        await _roomCommand.IncrementRoomMembersAsync(roomId, -1, innerCt).ConfigureAwait(false);
                    }

                    finalStatus = desiredStatus;
                }
            }

            return Result<RoomMemberStatus>.Success(finalStatus);
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
        if (actorUserId == targetUserId)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "Cannot remove yourself from the room."));
        }

        var room = await _roomQuery.GetRoomWithClubCommunityAsync(roomId, ct).ConfigureAwait(false);
        if (room is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Room not found."));
        }

        var actorMembership = await _roomQuery.GetMemberAsync(roomId, actorUserId, ct).ConfigureAwait(false);
        if (actorMembership is null || actorMembership.Status != RoomMemberStatus.Approved)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Room moderation privileges required."));
        }

        if (!HasModerationPrivileges(actorMembership.Role))
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Room moderation privileges required."));
        }

        var targetMembership = await _roomQuery.GetMemberAsync(roomId, targetUserId, ct).ConfigureAwait(false);
        if (targetMembership is not null && targetMembership.Role == RoomRole.Owner)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Cannot remove a room owner."));
        }

        if (targetMembership is not null && !HasHigherPriority(actorMembership.Role, targetMembership.Role))
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Cannot remove a member with equal or higher role."));
        }

        var kickResult = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            ClubMember? clubMembership = null;
            CommunityMember? communityMembership = null;

            if (room.ClubId != Guid.Empty)
            {
                clubMembership = await _clubQuery.GetMemberAsync(room.ClubId, targetUserId, innerCt).ConfigureAwait(false);
                if (clubMembership?.Role == MemberRole.Owner)
                {
                    return Result.Failure(new Error(Error.Codes.Forbidden, "Cannot remove a club owner."));
                }
            }

            var communityId = room.Club?.CommunityId;
            if (communityId.HasValue)
            {
                communityMembership = await _communityQuery
                    .GetMemberAsync(communityId.Value, targetUserId, innerCt)
                    .ConfigureAwait(false);

                if (communityMembership?.Role == MemberRole.Owner)
                {
                    return Result.Failure(new Error(Error.Codes.Forbidden, "Cannot remove a community owner."));
                }
            }

            if (targetMembership is not null)
            {
                var removedStatus = await _roomCommand.RemoveMemberAsync(roomId, targetUserId, innerCt).ConfigureAwait(false);

                if (removedStatus == RoomMemberStatus.Approved)
                {
                    await _roomCommand.IncrementRoomMembersAsync(roomId, -1, innerCt).ConfigureAwait(false);
                }
            }

            if (clubMembership is not null)
            {
                await _clubCommand.RemoveMemberAsync(room.ClubId, targetUserId, innerCt).ConfigureAwait(false);
                await _roomCommand.IncrementClubMembersAsync(room.ClubId, -1, innerCt).ConfigureAwait(false);
            }

            if (communityId.HasValue && communityMembership is not null)
            {
                await _communityCommand.RemoveMemberAsync(communityId.Value, targetUserId, innerCt).ConfigureAwait(false);
                await _roomCommand.IncrementCommunityMembersAsync(communityId.Value, -1, innerCt).ConfigureAwait(false);
            }

            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        if (kickResult.IsSuccess)
        {
            _logger.LogInformation(
                "Room member kick cascade succeeded. Source={Source}. Actor={ActorId}({ActorRole}) Target={TargetId}({TargetRole}) Room={RoomId} Club={ClubId} Community={CommunityId}",
                "CascadeFromRoom",
                actorUserId,
                actorMembership.Role,
                targetUserId,
                targetMembership?.Role,
                roomId,
                room.ClubId,
                room.Club?.CommunityId);
        }

        return kickResult;
    }

    private static bool HasModerationPrivileges(RoomRole role) => role != RoomRole.Member;

    private static bool HasHigherPriority(RoomRole actorRole, RoomRole targetRole)
    {
        return GetPriority(actorRole) > GetPriority(targetRole);
    }

    private static int GetPriority(RoomRole role) => role switch
    {
        RoomRole.Member => 0,
        RoomRole.Moderator => 1,
        RoomRole.Owner => 3,
        _ => 2
    };

    public async Task<Result> ApproveRoomMemberAsync(Guid roomId, Guid targetUserId, Guid actorUserId, CancellationToken ct = default)
    {
        var room = await _roomQuery.GetRoomWithClubCommunityAsync(roomId, ct).ConfigureAwait(false);
        if (room is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Room not found."));
        }

        var actorMembership = await _roomQuery.GetMemberAsync(roomId, actorUserId, ct).ConfigureAwait(false);
        if (actorMembership is null || actorMembership.Status != RoomMemberStatus.Approved || actorMembership.Role != RoomRole.Owner)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "OwnerOnly"));
        }

        var approveResult = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var status = await _roomCommand.GetMemberStatusAsync(roomId, targetUserId, innerCt).ConfigureAwait(false);
            if (status is null)
            {
                return Result.Failure(new Error(Error.Codes.NotFound, "Member not found."));
            }

            if (status == RoomMemberStatus.Approved)
            {
                return Result.Success();
            }

            if (status != RoomMemberStatus.Pending)
            {
                return Result.Failure(new Error(Error.Codes.Conflict, "InvalidMembershipState"));
            }

            if (room.Capacity.HasValue)
            {
                var approvedCount = await _roomCommand.CountApprovedMembersAsync(roomId, innerCt).ConfigureAwait(false);
                if (approvedCount >= room.Capacity.Value)
                {
                    return Result.Failure(new Error(Error.Codes.Conflict, "RoomCapacityExceeded"));
                }
            }

            var now = DateTime.UtcNow;
            await _roomCommand.UpdateMemberStatusAsync(roomId, targetUserId, RoomMemberStatus.Approved, now, actorUserId, innerCt).ConfigureAwait(false);
            await _roomCommand.IncrementRoomMembersAsync(roomId, 1, innerCt).ConfigureAwait(false);

            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        return approveResult;
    }

    public async Task<Result> RejectRoomMemberAsync(Guid roomId, Guid targetUserId, Guid actorUserId, CancellationToken ct = default)
    {
        var room = await _roomQuery.GetRoomWithClubCommunityAsync(roomId, ct).ConfigureAwait(false);
        if (room is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Room not found."));
        }

        var actorMembership = await _roomQuery.GetMemberAsync(roomId, actorUserId, ct).ConfigureAwait(false);
        if (actorMembership is null || actorMembership.Status != RoomMemberStatus.Approved || actorMembership.Role != RoomRole.Owner)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "OwnerOnly"));
        }

        var rejectResult = await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            var status = await _roomCommand.GetMemberStatusAsync(roomId, targetUserId, innerCt).ConfigureAwait(false);
            if (status is null)
            {
                return Result.Failure(new Error(Error.Codes.NotFound, "Member not found."));
            }

            if (status == RoomMemberStatus.Approved)
            {
                return Result.Failure(new Error(Error.Codes.Conflict, "InvalidMembershipState"));
            }

            var now = DateTime.UtcNow;
            await _roomCommand.UpdateMemberStatusAsync(roomId, targetUserId, RoomMemberStatus.Rejected, now, actorUserId, innerCt).ConfigureAwait(false);

            return Result.Success();
        }, ct: ct).ConfigureAwait(false);

        return rejectResult;
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

    public async Task<Result> DeleteRoomAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "RoomId is required."));
        }

        var room = await _roomQuery.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (room is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Room not found."));
        }

        var membership = await _roomQuery.GetMemberAsync(id, actorId, ct).ConfigureAwait(false);
        if (membership is null || membership.Status != RoomMemberStatus.Approved || membership.Role != RoomRole.Owner)
        {
            return Result.Failure(new Error(Error.Codes.Forbidden, "Only room owners can delete the room."));
        }

        return await _uow.ExecuteTransactionAsync(async innerCt =>
        {
            await _roomCommand.SoftDeleteRoomAsync(id, actorId, innerCt).ConfigureAwait(false);
            await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
            return Result.Success();
        }, ct: ct).ConfigureAwait(false);
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
