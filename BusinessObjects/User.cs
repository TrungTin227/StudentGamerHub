using Microsoft.AspNetCore.Identity;

namespace BusinessObjects;

// USER
public sealed class User : IdentityUser<Guid>, IAuditable, ISoftDelete
{
    // Profile
    public string? FullName { get; set; }
    public Gender? Gender { get; set; }
    public string? University { get; set; }
    public int Level { get; set; } = 1;

    // Media
    public string? AvatarUrl { get; set; }
    public string? CoverUrl { get; set; }

    // 1-1
    public NotificationPrefs NotificationPrefs { get; set; } = new();

    // Audit
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }

    // Navs
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new HashSet<RefreshToken>();
}

public sealed class UserRole : IdentityUserRole<Guid> { public User? User { get; set; } public Role? Role { get; set; } }
public sealed class UserClaim : IdentityUserClaim<Guid> { public User? User { get; set; } }
public sealed class RoleClaim : IdentityRoleClaim<Guid> { public Role? Role { get; set; } }
public sealed class UserLogin : IdentityUserLogin<Guid> { public User? User { get; set; } }
public sealed class UserToken : IdentityUserToken<Guid> { public User? User { get; set; } }
