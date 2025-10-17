using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Services.Common.Mapping
{
    public static class UserMappers
    {
        public static UserBriefDto ToUserBriefDto(this User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            return new UserBriefDto(
                Id: user.Id,
                UserName: user.UserName ?? string.Empty,
                AvatarUrl: user.AvatarUrl
            );
        }

        public static FriendDto ToFriendDtoFor(this FriendLink link, Guid requesterId)
        {
            ArgumentNullException.ThrowIfNull(link);

            if (link.SenderId == requesterId)
            {
                ArgumentNullException.ThrowIfNull(link.Recipient);
                return new FriendDto(
                    Id: link.Id,
                    User: link.Recipient.ToUserBriefDto(),
                    BecameFriendsAtUtc: link.RespondedAt
                );
            }

            if (link.RecipientId == requesterId)
            {
                ArgumentNullException.ThrowIfNull(link.Sender);
                return new FriendDto(
                    Id: link.Id,
                    User: link.Sender.ToUserBriefDto(),
                    BecameFriendsAtUtc: link.RespondedAt
                );
            }

            throw new InvalidOperationException("Requester must be associated with the friend link.");
        }

        /// <summary>
        /// Map entity -> UserListItemDto (UTC -> VN khi hiển thị).
        /// </summary>
        public static UserListItemDto ToListItemDto(
            this User u,
            ITimeZoneService tz,
            string[] roles,
            DateTime nowUtc)
        {
            bool isLocked = u.LockoutEnd != null && u.LockoutEnd.Value.UtcDateTime > nowUtc;

            return new UserListItemDto(
                Id: u.Id,
                UserName: u.UserName ?? string.Empty,
                Email: u.Email ?? string.Empty,
                FullName: u.FullName,
                Gender: u.Gender,
                University: u.University,
                Level: u.Level,
                AvatarUrl: u.AvatarUrl,
                PhoneNumber: u.PhoneNumber,
                EmailConfirmed: u.EmailConfirmed,
                IsLocked: isLocked,
                CreatedAtUtc: tz.ToVn(u.CreatedAtUtc),                    
                UpdatedAtUtc: tz.ToVn(u.UpdatedAtUtc ?? DateTime.MinValue),
                Roles: roles
            );
        }

        /// <summary>
        /// Map entity -> UserDetailDto (UTC -> VN khi hiển thị).
        /// </summary>
        public static async Task<UserDetailDto> ToDetailDtoAsync(
    this User u,
    UserManager<User> userManager,
    ITimeZoneService tz,
    CancellationToken ct = default)
        {
            var roles = await userManager.GetRolesAsync(u);

            DateTime? lockoutEndVnDateTime = u.LockoutEnd.HasValue
                ? tz.ToVn(u.LockoutEnd.Value.UtcDateTime)
                : null;

            var nowVn = tz.ToVn(DateTime.UtcNow);
            bool isLocked = lockoutEndVnDateTime.HasValue && lockoutEndVnDateTime.Value > nowVn;

            // 👇 Load thêm games nếu chưa được include
            var userWithGames = await userManager.Users
                .Include(x => x.UserGames)
                .ThenInclude(ug => ug.Game)
                .AsNoTracking()
                .FirstAsync(x => x.Id == u.Id, ct);

            var games = userWithGames.UserGames.Select(ug => new GameBriefDto(
                ug.GameId,
                ug.Game?.Name ?? string.Empty,
                ug.InGameName,
                ug.AddedAt,
                ug.Skill
            ));

            return new UserDetailDto(
                Id: u.Id,
                UserName: u.UserName ?? string.Empty,
                Email: u.Email ?? string.Empty,
                FullName: u.FullName,
                Gender: u.Gender,
                University: u.University,
                Level: u.Level,
                AvatarUrl: u.AvatarUrl,
                CoverUrl: u.CoverUrl,
                PhoneNumber: u.PhoneNumber,
                EmailConfirmed: u.EmailConfirmed,
                LockoutEndUtc: lockoutEndVnDateTime,
                IsLocked: isLocked,
                CreatedAtUtc: tz.ToVn(u.CreatedAtUtc),
                UpdatedAtUtc: tz.ToVn(u.UpdatedAtUtc ?? DateTime.MinValue),
                Roles: roles.ToArray(),
                Games: games 
            );
        }
    }
}