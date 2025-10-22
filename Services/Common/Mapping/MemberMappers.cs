using Repositories.Models;

namespace Services.Common.Mapping;

public static class MemberMappers
{
    public static CommunityMemberDto ToCommunityMemberDto(
        this CommunityMemberModel model,
        Guid? currentUserId)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new CommunityMemberDto
        {
            User = model.User.ToUserBriefDto(),
            Role = model.Role,
            JoinedAtUtc = model.JoinedAtUtc,
            IsOwner = model.Role == MemberRole.Owner,
            IsCurrentUser = currentUserId.HasValue && model.User.UserId == currentUserId.Value
        };
    }

    public static ClubMemberDto ToClubMemberDto(
        this ClubMemberModel model,
        Guid? currentUserId)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new ClubMemberDto
        {
            User = model.User.ToUserBriefDto(),
            Role = model.Role,
            JoinedAtUtc = model.JoinedAtUtc,
            IsOwner = model.Role == MemberRole.Owner,
            IsCurrentUser = currentUserId.HasValue && model.User.UserId == currentUserId.Value
        };
    }

    public static RoomMemberDto ToRoomMemberDto(
        this RoomMemberModel model,
        Guid? currentUserId)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new RoomMemberDto
        {
            User = model.User.ToUserBriefDto(),
            Role = model.Role,
            Status = model.Status,
            JoinedAtUtc = model.JoinedAtUtc,
            IsOwner = model.Role == RoomRole.Owner,
            IsCurrentUser = currentUserId.HasValue && model.User.UserId == currentUserId.Value
        };
    }
}
