namespace Repositories.Models;

public sealed record MemberUserModel(
    Guid UserId,
    string UserName,
    string? FullName,
    string? AvatarUrl,
    int Level);

public sealed record CommunityMemberModel(
    MemberUserModel User,
    MemberRole Role,
    DateTime JoinedAtUtc);

public sealed record ClubMemberModel(
    MemberUserModel User,
    MemberRole Role,
    DateTime JoinedAtUtc);

public sealed record RoomMemberModel(
    MemberUserModel User,
    RoomRole Role,
    RoomMemberStatus Status,
    DateTime JoinedAtUtc);
