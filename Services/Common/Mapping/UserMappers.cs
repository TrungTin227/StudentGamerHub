using Microsoft.AspNetCore.Identity;

namespace Services.Common.Mapping
{
    public static class UserMappers
    {
        /// <summary>
        /// Map entity -> UserListItemDto (UTC -> VN khi hiển thị).
        /// </summary>
        public static UserListItemDto ToListItemDto(
            this User u,
            ITimeZoneService tz,
            string[] roles,
            DateTimeOffset nowUtc)
        {
            bool isLocked = u.LockoutEnd != null && u.LockoutEnd > nowUtc;

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
                ? tz.ToVn(u.LockoutEnd.Value).DateTime
                : null;

            var nowVn = tz.ToVn(DateTime.UtcNow);
            bool isLocked = lockoutEndVnDateTime.HasValue && lockoutEndVnDateTime.Value > nowVn;

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
                Roles: roles.ToArray()
            );
        }
    }
}