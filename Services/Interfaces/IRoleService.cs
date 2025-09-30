
namespace Services.Interfaces
{
    public interface IRoleService : ICrudService<CreateRoleRequest, UpdateRoleRequest, RoleDto, Guid>, IScopedService
    {
        Task<Result<PagedResult<RoleDto>>> ListAsync(PageRequest paging, RoleFilter? filter, CancellationToken ct = default);
        Task<Result<bool>> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
    }
}
