using System.Linq.Expressions;

namespace Repositories.WorkSeeds.Interfaces
{
    public interface IGenericRepository<TEntity, TKey> where TEntity : class
    {
        // CRUD
        Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
        Task<TEntity> UpdateAsync(TEntity entity, CancellationToken ct = default);
        Task<bool> DeleteAsync(TKey id, CancellationToken ct = default);

        Task<TEntity?> GetByIdAsync(
            TKey id,
            bool asNoTracking = true,
            CancellationToken ct = default,
            params Expression<Func<TEntity, object>>[] includes);

        // Batch
        Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
        Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
        Task DeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken ct = default);
        Task<List<TEntity>> GetByIdsAsync(List<TKey> ids, bool asNoTracking = true, CancellationToken ct = default);

        // Query
        IQueryable<TEntity> GetQueryable(bool asNoTracking = true);
        Task<PagedResult<TEntity>> GetPagedAsync(
            PageRequest request,
            Expression<Func<TEntity, bool>>? predicate = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
            bool asNoTracking = true,
            DeletedFilter deleted = DeletedFilter.OnlyActive,

            CancellationToken ct = default,
            params Expression<Func<TEntity, object>>[] includes);

        Task<IReadOnlyList<TEntity>> GetAllAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
            bool asNoTracking = true,
            CancellationToken ct = default,
            params Expression<Func<TEntity, object>>[] includes);   

        Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            bool asNoTracking = true,
            CancellationToken ct = default,
            params Expression<Func<TEntity, object>>[] includes);

        Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
        Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default);

        // Soft delete (nếu entity có ISoftDelete)
        Task<bool> SoftDeleteAsync(TKey id, Guid? deletedBy = null, CancellationToken ct = default);
        Task<bool> RestoreAsync(TKey id, Guid? restoredBy = null, CancellationToken ct = default);
    }
}
