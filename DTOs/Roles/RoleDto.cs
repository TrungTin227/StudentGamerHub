namespace DTOs.Roles
{
    public sealed record RoleDto(
        Guid Id,
        string Name,
        string? Description,
        DateTime CreatedAtUtc,
        Guid? CreatedBy,
        DateTime? UpdatedAtUtc,
        Guid? UpdatedBy,
        bool IsDeleted,
        DateTime? DeletedAtUtc,
        Guid? DeletedBy
    );
}
