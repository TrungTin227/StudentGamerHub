namespace Repositories.Interfaces
{
    public interface IRoleRepository : IGenericRepository<Role, Guid>
    {
        Task<Role?> FindByNameAsync(string name,
            DeletedFilter deleted = DeletedFilter.OnlyActive,
            bool asNoTracking = true, CancellationToken ct = default);

        Task<Role?> FindByNormalizedNameAsync(string normalizedName,
            DeletedFilter deleted = DeletedFilter.OnlyActive,
            bool asNoTracking = true, CancellationToken ct = default);

        Task<bool> NameExistsAsync(string name, Guid? excludeId = null,
            DeletedFilter deleted = DeletedFilter.OnlyActive, CancellationToken ct = default);

        Task<bool> NormalizedNameExistsAsync(string normalizedName, Guid? excludeId = null,
            DeletedFilter deleted = DeletedFilter.OnlyActive, CancellationToken ct = default);

        Task<Role?> GetWithClaimsAsync(Guid id, bool asNoTracking = true, CancellationToken ct = default);
        Task<Role?> GetWithUsersAsync(Guid id, bool asNoTracking = true, CancellationToken ct = default);

        Task<PagedResult<Role>> SearchPagedAsync(PageRequest request,
            RoleFilter? filter = null, CancellationToken ct = default);
    }
}
