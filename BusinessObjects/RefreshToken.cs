namespace BusinessObjects;

public sealed class RefreshToken : AuditableEntity
{
    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }

    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReasonRevoked { get; set; }
    public Guid? ReplacedByTokenId { get; set; }  // rotation chain

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;

    public User? User { get; set; }
}
