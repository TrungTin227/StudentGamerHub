using Microsoft.AspNetCore.Identity;

namespace Services.Implementations;

/// <summary>
/// Room service implementation.
/// Manages room creation, joining, member approval, leaving, and moderation.
/// All write operations are wrapped in transactions via IGenericUnitOfWork.
/// </summary>
public sealed class RoomService : IRoomService
{
    private readonly IGenericUnitOfWork _uow;
    private readonly IRoomQueryRepository _roomQuery;
    private readonly IRoomCommandRepository _roomCommand;
    private readonly IPasswordHasher<Room> _passwordHasher;

    public RoomService(
        IGenericUnitOfWork uow,
        IRoomQueryRepository roomQuery,
        IRoomCommandRepository roomCommand,
        IPasswordHasher<Room> passwordHasher)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _roomQuery = roomQuery ?? throw new ArgumentNullException(nameof(roomQuery));
        _roomCommand = roomCommand ?? throw new ArgumentNullException(nameof(roomCommand));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    /// <inheritdoc/>
    public async Task<Result<Guid>> CreateRoomAsync(
        Guid currentUserId,
        Guid clubId,
        string name,
        string? description,
        RoomJoinPolicy policy,
        string? password,
        int? capacity,
        CancellationToken ct = default)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(name))
            return Result<Guid>.Failure(new Error(Error.Codes.Validation, "Room name is required."));

        if (policy == RoomJoinPolicy.RequiresPassword && string.IsNullOrWhiteSpace(password))
            return Result<Guid>.Failure(new Error(Error.Codes.Validation, "Password is required for RequiresPassword policy."));

        if (capacity.HasValue && capacity.Value < 1)
            return Result<Guid>.Failure(new Error(Error.Codes.Validation, "Capacity must be at least 1."));

        return await _uow.ExecuteTransactionAsync<Guid>(async ctk =>
        {
            // Create room
            var room = new Room
            {
                Id = Guid.NewGuid(),
                ClubId = clubId,
                Name = name.Trim(),
                Description = description?.Trim(),
                JoinPolicy = policy,
                Capacity = capacity,
                MembersCount = 0
            };

            // Hash password if needed
            if (policy == RoomJoinPolicy.RequiresPassword && !string.IsNullOrWhiteSpace(password))
            {
                room.JoinPasswordHash = _passwordHasher.HashPassword(room, password);
            }

            await _roomCommand.CreateRoomAsync(room, ctk);

            // Create owner member (Approved)
            // Note: RoomMember uses composite PK (RoomId, UserId), no Id assignment needed
            var ownerMember = new RoomMember
            {
                RoomId = room.Id,
                UserId = currentUserId,
                Role = RoomRole.Owner,
                Status = RoomMemberStatus.Approved,
                JoinedAt = DateTimeOffset.UtcNow
            };

            await _roomCommand.UpsertMemberAsync(ownerMember, ctk);

            // Update counters
            await _roomCommand.IncrementRoomMembersAsync(room.Id, 1, ctk);

            // Check if this is the first approved member in club/community
            var hasOtherInClub = await _roomQuery.HasAnyApprovedInClubAsync(currentUserId, clubId, ctk);
            if (!hasOtherInClub)
            {
                // This is the first approved member in the club
                await _roomCommand.IncrementClubMembersAsync(clubId, 1, ctk);

                // Load room to get community ID
                var roomWithClub = await _roomQuery.GetRoomWithClubCommunityAsync(room.Id, ctk);
                if (roomWithClub?.Club?.CommunityId is Guid communityId)
                {
                    var hasOtherInCommunity = await _roomQuery.HasAnyApprovedInCommunityAsync(currentUserId, communityId, ctk);
                    if (!hasOtherInCommunity)
                    {
                        // This is the first approved member in the community
                        await _roomCommand.IncrementCommunityMembersAsync(communityId, 1, ctk);
                    }
                }
            }

            await _uow.SaveChangesAsync(ctk);

            return Result<Guid>.Success(room.Id);
        }, ct: ct);
    }

    /// <inheritdoc/>
    public async Task<Result> JoinRoomAsync(
        Guid currentUserId,
        Guid roomId,
        string? password,
        CancellationToken ct = default)
    {
        return await _uow.ExecuteTransactionAsync(async ctk =>
        {
            // Load room with club and community
            var room = await _roomQuery.GetRoomWithClubCommunityAsync(roomId, ctk);
            if (room is null)
                return Result.Failure(new Error(Error.Codes.NotFound, "Room not found."));

            // Check existing membership
            var existingMember = await _roomQuery.GetMemberAsync(roomId, currentUserId, ctk);
            if (existingMember is not null)
            {
                if (existingMember.Status == RoomMemberStatus.Approved)
                    return Result.Failure(new Error(Error.Codes.Conflict, "Already a member of this room."));
                
                if (existingMember.Status == RoomMemberStatus.Pending)
                    return Result.Failure(new Error(Error.Codes.Conflict, "Membership is pending approval."));
                
                if (existingMember.Status == RoomMemberStatus.Banned)
                    return Result.Failure(new Error(Error.Codes.Forbidden, "You are banned from this room."));
            }

            // Determine status based on policy
            RoomMemberStatus status;
            switch (room.JoinPolicy)
            {
                case RoomJoinPolicy.Open:
                    // Check capacity before approving
                    if (room.Capacity.HasValue)
                    {
                        var approvedCount = await _roomQuery.CountApprovedMembersAsync(roomId, ctk);
                        if (approvedCount >= room.Capacity.Value)
                            return Result.Failure(new Error(Error.Codes.Conflict, "Room is at full capacity."));
                    }
                    status = RoomMemberStatus.Approved;
                    break;

                case RoomJoinPolicy.RequiresApproval:
                    status = RoomMemberStatus.Pending;
                    break;

                case RoomJoinPolicy.RequiresPassword:
                    // Verify password
                    if (string.IsNullOrWhiteSpace(password))
                        return Result.Failure(new Error(Error.Codes.Validation, "Password is required."));

                    if (string.IsNullOrWhiteSpace(room.JoinPasswordHash))
                        return Result.Failure(new Error(Error.Codes.Unexpected, "Room password is not configured."));

                    var verifyResult = _passwordHasher.VerifyHashedPassword(room, room.JoinPasswordHash, password);
                    if (verifyResult == PasswordVerificationResult.Failed)
                        return Result.Failure(new Error(Error.Codes.Forbidden, "Invalid password."));

                    // Check capacity before approving
                    if (room.Capacity.HasValue)
                    {
                        var approvedCount = await _roomQuery.CountApprovedMembersAsync(roomId, ctk);
                        if (approvedCount >= room.Capacity.Value)
                            return Result.Failure(new Error(Error.Codes.Conflict, "Room is at full capacity."));
                    }
                    status = RoomMemberStatus.Approved;
                    break;

                default:
                    return Result.Failure(new Error(Error.Codes.Unexpected, "Invalid room join policy."));
            }

            // Create or update member
            // Note: RoomMember uses composite PK (RoomId, UserId), no Id assignment needed
            var member = new RoomMember
            {
                RoomId = roomId,
                UserId = currentUserId,
                Role = RoomRole.Member,
                Status = status,
                JoinedAt = DateTimeOffset.UtcNow
            };

            await _roomCommand.UpsertMemberAsync(member, ctk);

            // Update counters only if approved
            if (status == RoomMemberStatus.Approved)
            {
                await _roomCommand.IncrementRoomMembersAsync(roomId, 1, ctk);

                var clubId = room.ClubId;
                var hasOtherInClub = await _roomQuery.HasAnyApprovedInClubAsync(currentUserId, clubId, ctk);
                if (!hasOtherInClub)
                {
                    await _roomCommand.IncrementClubMembersAsync(clubId, 1, ctk);

                    if (room.Club?.CommunityId is Guid communityId)
                    {
                        var hasOtherInCommunity = await _roomQuery.HasAnyApprovedInCommunityAsync(currentUserId, communityId, ctk);
                        if (!hasOtherInCommunity)
                        {
                            await _roomCommand.IncrementCommunityMembersAsync(communityId, 1, ctk);
                        }
                    }
                }
            }

            await _uow.SaveChangesAsync(ctk);

            return Result.Success();
        }, ct: ct);
    }

    /// <inheritdoc/>
    public async Task<Result> ApproveMemberAsync(
        Guid currentUserId,
        Guid roomId,
        Guid targetUserId,
        CancellationToken ct = default)
    {
        return await _uow.ExecuteTransactionAsync(async ctk =>
        {
            // Check if current user has permission (Owner or Moderator)
            var currentMember = await _roomQuery.GetMemberAsync(roomId, currentUserId, ctk);
            if (currentMember is null || currentMember.Status != RoomMemberStatus.Approved)
                return Result.Failure(new Error(Error.Codes.Forbidden, "You are not a member of this room."));

            if (currentMember.Role != RoomRole.Owner && currentMember.Role != RoomRole.Moderator)
                return Result.Failure(new Error(Error.Codes.Forbidden, "Only owners and moderators can approve members."));

            // Get target member
            var targetMember = await _roomQuery.GetMemberAsync(roomId, targetUserId, ctk);
            if (targetMember is null)
                return Result.Failure(new Error(Error.Codes.NotFound, "Member not found."));

            if (targetMember.Status != RoomMemberStatus.Pending)
                return Result.Failure(new Error(Error.Codes.Conflict, "Member is not pending approval."));

            // Load room to check capacity and get club/community
            var room = await _roomQuery.GetRoomWithClubCommunityAsync(roomId, ctk);
            if (room is null)
                return Result.Failure(new Error(Error.Codes.NotFound, "Room not found."));

            // Check capacity
            if (room.Capacity.HasValue)
            {
                var approvedCount = await _roomQuery.CountApprovedMembersAsync(roomId, ctk);
                if (approvedCount >= room.Capacity.Value)
                    return Result.Failure(new Error(Error.Codes.Conflict, "Room is at full capacity."));
            }

            // Approve member
            targetMember.Status = RoomMemberStatus.Approved;
            await _roomCommand.UpdateMemberAsync(targetMember, ctk);

            // Update counters
            await _roomCommand.IncrementRoomMembersAsync(roomId, 1, ctk);

            var clubId = room.ClubId;
            var hasOtherInClub = await _roomQuery.HasAnyApprovedInClubAsync(targetUserId, clubId, ctk);
            if (!hasOtherInClub)
            {
                await _roomCommand.IncrementClubMembersAsync(clubId, 1, ctk);

                if (room.Club?.CommunityId is Guid communityId)
                {
                    var hasOtherInCommunity = await _roomQuery.HasAnyApprovedInCommunityAsync(targetUserId, communityId, ctk);
                    if (!hasOtherInCommunity)
                    {
                        await _roomCommand.IncrementCommunityMembersAsync(communityId, 1, ctk);
                    }
                }
            }

            await _uow.SaveChangesAsync(ctk);

            return Result.Success();
        }, ct: ct);
    }

    /// <inheritdoc/>
    public async Task<Result> LeaveRoomAsync(
        Guid currentUserId,
        Guid roomId,
        CancellationToken ct = default)
    {
        return await _uow.ExecuteTransactionAsync(async ctk =>
        {
            // Get member
            var member = await _roomQuery.GetMemberAsync(roomId, currentUserId, ctk);
            if (member is null)
                return Result.Failure(new Error(Error.Codes.NotFound, "You are not a member of this room."));

            // Owner cannot leave (MVP restriction)
            if (member.Role == RoomRole.Owner)
                return Result.Failure(new Error(Error.Codes.Forbidden, "Owner cannot leave the room."));

            var wasApproved = member.Status == RoomMemberStatus.Approved;

            // Remove member
            await _roomCommand.RemoveMemberAsync(roomId, currentUserId, ctk);

            // Update counters only if was approved
            if (wasApproved)
            {
                await _roomCommand.IncrementRoomMembersAsync(roomId, -1, ctk);

                // Load room to get club/community
                var room = await _roomQuery.GetRoomWithClubCommunityAsync(roomId, ctk);
                if (room is not null)
                {
                    var clubId = room.ClubId;
                    
                    // Check if user has any other approved memberships in this club
                    var hasOtherInClub = await _roomQuery.HasAnyApprovedInClubAsync(currentUserId, clubId, ctk);
                    if (!hasOtherInClub)
                    {
                        // This was the last approved membership in the club
                        await _roomCommand.IncrementClubMembersAsync(clubId, -1, ctk);

                        if (room.Club?.CommunityId is Guid communityId)
                        {
                            var hasOtherInCommunity = await _roomQuery.HasAnyApprovedInCommunityAsync(currentUserId, communityId, ctk);
                            if (!hasOtherInCommunity)
                            {
                                // This was the last approved membership in the community
                                await _roomCommand.IncrementCommunityMembersAsync(communityId, -1, ctk);
                            }
                        }
                    }
                }
            }

            await _uow.SaveChangesAsync(ctk);

            return Result.Success();
        }, ct: ct);
    }

    /// <inheritdoc/>
    public async Task<Result> KickOrBanAsync(
        Guid currentUserId,
        Guid roomId,
        Guid targetUserId,
        bool ban,
        CancellationToken ct = default)
    {
        return await _uow.ExecuteTransactionAsync(async ctk =>
        {
            // Check if current user has permission (Owner or Moderator)
            var currentMember = await _roomQuery.GetMemberAsync(roomId, currentUserId, ctk);
            if (currentMember is null || currentMember.Status != RoomMemberStatus.Approved)
                return Result.Failure(new Error(Error.Codes.Forbidden, "You are not a member of this room."));

            if (currentMember.Role != RoomRole.Owner && currentMember.Role != RoomRole.Moderator)
                return Result.Failure(new Error(Error.Codes.Forbidden, "Only owners and moderators can kick or ban members."));

            // Get target member
            var targetMember = await _roomQuery.GetMemberAsync(roomId, targetUserId, ctk);
            if (targetMember is null)
                return Result.Failure(new Error(Error.Codes.NotFound, "Member not found."));

            // Cannot kick/ban owner
            if (targetMember.Role == RoomRole.Owner)
                return Result.Failure(new Error(Error.Codes.Forbidden, "Cannot kick or ban the room owner."));

            var wasApproved = targetMember.Status == RoomMemberStatus.Approved;

            // Update status
            targetMember.Status = ban ? RoomMemberStatus.Banned : RoomMemberStatus.Rejected;
            await _roomCommand.UpdateMemberAsync(targetMember, ctk);

            // Update counters only if was approved
            if (wasApproved)
            {
                await _roomCommand.IncrementRoomMembersAsync(roomId, -1, ctk);

                // Load room to get club/community
                var room = await _roomQuery.GetRoomWithClubCommunityAsync(roomId, ctk);
                if (room is not null)
                {
                    var clubId = room.ClubId;
                    
                    // Check if user has any other approved memberships in this club
                    var hasOtherInClub = await _roomQuery.HasAnyApprovedInClubAsync(targetUserId, clubId, ctk);
                    if (!hasOtherInClub)
                    {
                        // This was the last approved membership in the club
                        await _roomCommand.IncrementClubMembersAsync(clubId, -1, ctk);

                        if (room.Club?.CommunityId is Guid communityId)
                        {
                            var hasOtherInCommunity = await _roomQuery.HasAnyApprovedInCommunityAsync(targetUserId, communityId, ctk);
                            if (!hasOtherInCommunity)
                            {
                                // This was the last approved membership in the community
                                await _roomCommand.IncrementCommunityMembersAsync(communityId, -1, ctk);
                            }
                        }
                    }
                }
            }

            await _uow.SaveChangesAsync(ctk);

            return Result.Success();
        }, ct: ct);
    }

    /// <inheritdoc />
    public async Task<Result<RoomDetailDto>> GetByIdAsync(Guid roomId, CancellationToken ct = default)
    {
        var room = await _roomQuery.GetRoomWithClubCommunityAsync(roomId, ct);

        if (room is null)
            return Result<RoomDetailDto>.Failure(new Error(Error.Codes.NotFound, "Room not found."));

        return Result<RoomDetailDto>.Success(room.ToRoomDetailDto());
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<RoomMemberBriefDto>>> ListMembersAsync(
        Guid roomId,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        if (skip < 0)
            return Result<IReadOnlyList<RoomMemberBriefDto>>.Failure(new Error(Error.Codes.Validation, "skip must be >= 0."));

        if (take <= 0 || take > 100)
            return Result<IReadOnlyList<RoomMemberBriefDto>>.Failure(new Error(Error.Codes.Validation, "take must be between 1 and 100."));

        var room = await _roomQuery.GetByIdAsync(roomId, ct);
        if (room is null)
            return Result<IReadOnlyList<RoomMemberBriefDto>>.Failure(new Error(Error.Codes.NotFound, "Room not found."));

        var members = await _roomQuery.ListMembersAsync(roomId, take, skip, ct);
        var dtos = members.Select(m => m.ToRoomMemberBriefDto()).ToList();

        return Result<IReadOnlyList<RoomMemberBriefDto>>.Success(dtos);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateRoomAsync(
        Guid currentUserId,
        Guid roomId,
        RoomUpdateRequestDto req,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (string.IsNullOrWhiteSpace(req.Name))
            return Result.Failure(new Error(Error.Codes.Validation, "Room name is required."));

        if (req.Capacity.HasValue && req.Capacity.Value < 1)
            return Result.Failure(new Error(Error.Codes.Validation, "Capacity must be at least 1 when provided."));

        if (req.JoinPolicy == RoomJoinPolicy.RequiresPassword && string.IsNullOrWhiteSpace(req.Password))
            return Result.Failure(new Error(Error.Codes.Validation, "Password is required for RequiresPassword policy."));

        var trimmedName = req.Name.Trim();
        var normalizedDescription = NormalizeOrNull(req.Description);
        var trimmedPassword = req.Password?.Trim();

        return await _uow.ExecuteTransactionAsync(async ctk =>
        {
            var room = await _roomQuery.GetByIdAsync(roomId, ctk);
            if (room is null)
                return Result.Failure(new Error(Error.Codes.NotFound, "Room not found."));

            var member = await _roomQuery.GetMemberAsync(roomId, currentUserId, ctk);
            if (member is null || member.Status != RoomMemberStatus.Approved)
                return Result.Failure(new Error(Error.Codes.Forbidden, "You are not a member of this room."));

            if (member.Role != RoomRole.Owner)
                return Result.Failure(new Error(Error.Codes.Forbidden, "Only the owner can update the room."));

            if (req.Capacity.HasValue)
            {
                var approvedCount = await _roomQuery.CountApprovedMembersAsync(roomId, ctk);
                if (approvedCount > req.Capacity.Value)
                    return Result.Failure(new Error(Error.Codes.Conflict, "Capacity cannot be lower than current approved members."));
            }

            room.Name = trimmedName;
            room.Description = normalizedDescription;
            room.JoinPolicy = req.JoinPolicy;
            room.Capacity = req.Capacity;
            room.UpdatedAtUtc = DateTime.UtcNow;
            room.UpdatedBy = currentUserId;

            if (req.JoinPolicy == RoomJoinPolicy.RequiresPassword)
            {
                room.JoinPasswordHash = _passwordHasher.HashPassword(room, trimmedPassword!);
            }
            else
            {
                room.JoinPasswordHash = null;
            }

            await _roomCommand.UpdateRoomAsync(room, ctk);
            await _uow.SaveChangesAsync(ctk);

            return Result.Success();
        }, ct: ct);
    }

    /// <inheritdoc />
    public async Task<Result> TransferOwnershipAsync(
        Guid currentUserId,
        Guid roomId,
        Guid newOwnerUserId,
        CancellationToken ct = default)
    {
        return await _uow.ExecuteTransactionAsync(async ctk =>
        {
            var room = await _roomQuery.GetByIdAsync(roomId, ctk);
            if (room is null)
                return Result.Failure(new Error(Error.Codes.NotFound, "Room not found."));

            var currentMember = await _roomQuery.GetMemberAsync(roomId, currentUserId, ctk);
            if (currentMember is null || currentMember.Status != RoomMemberStatus.Approved)
                return Result.Failure(new Error(Error.Codes.Forbidden, "You are not a member of this room."));

            if (currentMember.Role != RoomRole.Owner)
                return Result.Failure(new Error(Error.Codes.Forbidden, "Only the owner can transfer ownership."));

            var targetMember = await _roomQuery.GetMemberAsync(roomId, newOwnerUserId, ctk);
            if (targetMember is null)
                return Result.Failure(new Error(Error.Codes.NotFound, "Target member not found."));

            if (targetMember.Status != RoomMemberStatus.Approved)
                return Result.Failure(new Error(Error.Codes.Conflict, "Target member must be approved."));

            currentMember.Role = RoomRole.Moderator;
            targetMember.Role = RoomRole.Owner;

            await _roomCommand.UpdateMemberAsync(currentMember, ctk);
            await _roomCommand.UpdateMemberAsync(targetMember, ctk);
            await _uow.SaveChangesAsync(ctk);

            return Result.Success();
        }, ct: ct);
    }

    /// <inheritdoc />
    public async Task<Result> ArchiveRoomAsync(
        Guid currentUserId,
        Guid roomId,
        CancellationToken ct = default)
    {
        return await _uow.ExecuteTransactionAsync(async ctk =>
        {
            var room = await _roomQuery.GetByIdAsync(roomId, ctk);
            if (room is null)
                return Result.Failure(new Error(Error.Codes.NotFound, "Room not found."));

            var member = await _roomQuery.GetMemberAsync(roomId, currentUserId, ctk);
            if (member is null || member.Status != RoomMemberStatus.Approved)
                return Result.Failure(new Error(Error.Codes.Forbidden, "You are not a member of this room."));

            if (member.Role != RoomRole.Owner)
                return Result.Failure(new Error(Error.Codes.Forbidden, "Only the owner can archive the room."));

            var approvedCount = await _roomQuery.CountApprovedMembersAsync(roomId, ctk);
            if (approvedCount > 1)
                return Result.Failure(new Error(Error.Codes.Forbidden, "Room still has other approved members."));

            var members = await _roomQuery.ListMembersAsync(roomId, int.MaxValue, 0, ctk);
            var approvedMembers = members
                .Where(m => m.Status == RoomMemberStatus.Approved)
                .Select(m => m.UserId)
                .ToList();

            await _roomCommand.SoftDeleteRoomAsync(roomId, currentUserId, ctk);
            await _uow.SaveChangesAsync(ctk);

            foreach (var userId in approvedMembers)
            {
                var hasOtherInClub = await _roomQuery.HasAnyApprovedInClubAsync(userId, room.ClubId, ctk);
                if (!hasOtherInClub)
                {
                    await _roomCommand.IncrementClubMembersAsync(room.ClubId, -1, ctk);

                    if (room.Club?.CommunityId is Guid communityId)
                    {
                        var hasOtherInCommunity = await _roomQuery.HasAnyApprovedInCommunityAsync(userId, communityId, ctk);
                        if (!hasOtherInCommunity)
                        {
                            await _roomCommand.IncrementCommunityMembersAsync(communityId, -1, ctk);
                        }
                    }
                }
            }

            await _uow.SaveChangesAsync(ctk);

            return Result.Success();
        }, ct: ct);
    }

    private static string? NormalizeOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
