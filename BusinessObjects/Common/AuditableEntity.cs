namespace BusinessObjects.Common;

public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    Guid? CreatedBy { get; set; }
    DateTime? UpdatedAtUtc { get; set; }
    Guid? UpdatedBy { get; set; }
}

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAtUtc { get; set; }
    Guid? DeletedBy { get; set; }
}

public abstract class AuditableEntity : IAuditable, ISoftDelete
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
}
