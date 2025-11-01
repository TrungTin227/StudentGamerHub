using DTOs.Games;
using DTOs.Memberships;

namespace DTOs.Users
{
    public sealed record UserListItemDto(
        Guid Id,
        string UserName,
        string Email,
        string? FullName,
        Gender? Gender,
        string? University,
        int Level,
        string? AvatarUrl,
        string? PhoneNumber,
        bool EmailConfirmed,
        bool IsLocked,
        DateTime CreatedAtUtc,
        DateTime? UpdatedAtUtc,
        string[] Roles
    );

    public sealed record UserDetailDto(
    Guid Id,
    string UserName,
    string Email,
    string? FullName,
    Gender? Gender,
    string? University,
    int Level,
    string? AvatarUrl,
    string? CoverUrl,
    string? PhoneNumber,
    bool EmailConfirmed,
    DateTime? LockoutEndUtc,
    bool IsLocked,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string[] Roles,
    IEnumerable<GameBriefDto> Games,
    UserMembershipInfoDto? ActiveMembership 
);

}

