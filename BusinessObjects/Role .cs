using Microsoft.AspNetCore.Identity;

namespace BusinessObjects;

// ROLE
public sealed class Role : IdentityRole<Guid>, IAuditable, ISoftDelete
{
    public string? Description { get; set; }

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
    public ICollection<RoleClaim> RoleClaims { get; set; } = new HashSet<RoleClaim>();
    public ICollection<UserRole> UserRoles { get; set; } = new HashSet<UserRole>();
}
