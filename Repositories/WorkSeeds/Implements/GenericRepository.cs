using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Repositories.WorkSeeds.Implements
{
    public class GenericRepository<TEntity, TKey> : IGenericRepository<TEntity, TKey>
        where TEntity : class
    {
        protected readonly AppDbContext _context;
        protected readonly DbSet<TEntity> _dbSet;

        public GenericRepository(AppDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<TEntity>();
        }

        // CRUD ---------------------------------------------------------

        public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
        {
            await _dbSet.AddAsync(entity, ct);
            return entity;
        }

        public virtual Task<TEntity> UpdateAsync(TEntity entity, CancellationToken ct = default)
        {
            _dbSet.Update(entity);
            return Task.FromResult(entity);
        }

        public virtual async Task<bool> DeleteAsync(TKey id, CancellationToken ct = default)
        {
            // Nếu chưa bật C# 12: dùng new object?[] { id }
#if NET8_0_OR_GREATER
            var entity = await _dbSet.FindAsync([id], ct);
#else
            var entity = await _dbSet.FindAsync(new object?[] { id }, ct);
#endif
            if (entity is null) return false;

            _dbSet.Remove(entity);
            return true;
        }

        public virtual async Task<TEntity?> GetByIdAsync(
            TKey id,
            bool asNoTracking = true,
            CancellationToken ct = default,
            params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> query = _dbSet;
            if (asNoTracking) query = query.AsNoTracking();

            foreach (var include in includes)
                query = query.Include(include);

            return await query.FirstOrDefaultAsync(e => EF.Property<TKey>(e, "Id")!.Equals(id), ct);
        }

        // Batch --------------------------------------------------------

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
            => await _dbSet.AddRangeAsync(entities, ct);

        public virtual Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            _dbSet.UpdateRange(entities);
            return Task.CompletedTask;
        }

        public virtual async Task DeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken ct = default)
        {
            var list = await _dbSet
                .Where(e => ids.Contains(EF.Property<TKey>(e, "Id")))
                .ToListAsync(ct);
            _dbSet.RemoveRange(list);
        }

        public virtual async Task<List<TEntity>> GetByIdsAsync(List<TKey> ids, bool asNoTracking = true, CancellationToken ct = default)
        {
            IQueryable<TEntity> q = _dbSet;
            if (asNoTracking) q = q.AsNoTracking();

            return await q.Where(e => ids.Contains(EF.Property<TKey>(e, "Id"))).ToListAsync(ct);
        }

        // Query --------------------------------------------------------

        public virtual IQueryable<TEntity> GetQueryable(bool asNoTracking = true)
            => asNoTracking ? _dbSet.AsNoTracking() : _dbSet.AsQueryable();

        public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
            bool asNoTracking = true,
            CancellationToken ct = default,
            params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> q = _dbSet;
            if (asNoTracking) q = q.AsNoTracking();

            if (predicate is not null) q = q.Where(predicate);
            foreach (var include in includes) q = q.Include(include);
            if (orderBy is not null) q = orderBy(q);

            return await q.ToListAsync(ct);
        }

        // ✅ CHUẨN: nhận PageRequest
        public virtual async Task<PagedResult<TEntity>> GetPagedAsync(
            PageRequest request,
            Expression<Func<TEntity, bool>>? predicate = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
            bool asNoTracking = true,
            DeletedFilter deleted = DeletedFilter.OnlyActive,
            CancellationToken ct = default,
            params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> q = _dbSet;

            if (asNoTracking) q = q.AsNoTracking();

            // 👉 Áp dụng filter xóa mềm
            q = ApplyDeletedFilter(q, deleted);

            if (predicate is not null) q = q.Where(predicate);
            foreach (var include in includes) q = q.Include(include);

            if (orderBy is not null)
            {
                var ordered = orderBy(q);

                var total = await ordered.CountAsync(ct);
                var page = request.PageSafe;
                var size = request.SizeSafe;

                var totalPages = (int)Math.Ceiling(total / (double)size);
                var items = await ordered
                    .Skip((page - 1) * size)
                    .Take(size)
                    .ToListAsync(ct);

                return new PagedResult<TEntity>(
                    Items: items,
                    Page: page,
                    Size: size,
                    TotalCount: total,
                    TotalPages: totalPages,
                    HasPrevious: page > 1,
                    HasNext: page < totalPages,
                    Sort: request.SortSafe,
                    Desc: request.Desc
                );
            }

            // Không truyền orderBy → dùng hệ sort động + phân trang chuẩn
            return await q.ToPagedResultAsync(request, ct);
        }

        public virtual async Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            bool asNoTracking = true,
            CancellationToken ct = default,
            params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> q = _dbSet;
            if (asNoTracking) q = q.AsNoTracking();
            foreach (var include in includes) q = q.Include(include);

            return await q.FirstOrDefaultAsync(predicate, ct);
        }

        public virtual Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
            => _dbSet.AnyAsync(predicate, ct);

        public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default)
            => predicate is null ? await _dbSet.CountAsync(ct) : await _dbSet.CountAsync(predicate, ct);

        // Soft delete ---------------------------------------------------

        public virtual async Task<bool> SoftDeleteAsync(TKey id, Guid? deletedBy = null, CancellationToken ct = default)
        {
#if NET8_0_OR_GREATER
            var entity = await _dbSet.FindAsync([id], ct);
#else
            var entity = await _dbSet.FindAsync(new object?[] { id }, ct);
#endif
            if (entity is null) return false;

            if (entity is ISoftDelete sd)
            {
                sd.IsDeleted = true;
                sd.DeletedAtUtc = DateTime.UtcNow;
                sd.DeletedBy = deletedBy;

                if (entity is IAuditable aud && deletedBy.HasValue)
                {
                    aud.UpdatedAtUtc = DateTime.UtcNow;
                    aud.UpdatedBy = deletedBy;
                }

                _dbSet.Update(entity);
                return true;
            }

            _dbSet.Remove(entity); // không support soft delete -> hard delete
            return true;
        }

        public virtual async Task<bool> RestoreAsync(TKey id, Guid? restoredBy = null, CancellationToken ct = default)
        {
            var entity = await _dbSet
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => EF.Property<TKey>(e, "Id")!.Equals(id), ct);

            if (entity is null) return false;

            if (entity is ISoftDelete sd)
            {
                sd.IsDeleted = false;
                sd.DeletedAtUtc = null;
                sd.DeletedBy = null;

                if (entity is IAuditable aud && restoredBy.HasValue)
                {
                    aud.UpdatedAtUtc = DateTime.UtcNow;
                    aud.UpdatedBy = restoredBy;
                }

                _dbSet.Update(entity);
                return true;
            }

            return false;
        }
        // Helpers ------------------------------------------------------
        // Helper: áp dụng filter xóa mềm theo enum
        private static IQueryable<TEntity> ApplyDeletedFilter(
            IQueryable<TEntity> q, DeletedFilter filter)
        {
            // Nếu entity không hỗ trợ soft delete thì bỏ qua
            var supportsSoftDelete =
                typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity))
                // hoặc cho trường hợp không implements interface mà vẫn có cột IsDeleted
                || typeof(TEntity).GetProperty("IsDeleted") is not null;

            if (!supportsSoftDelete) return q;

            return filter switch
            {
                DeletedFilter.All =>
                    q.IgnoreQueryFilters(), // bỏ mọi Global Query Filters

                DeletedFilter.OnlyDeleted =>
                    q.IgnoreQueryFilters()
                     .Where(e => EF.Property<bool>(e, "IsDeleted") == true),

                _ => q // OnlyActive: để nguyên (tôn trọng Global Query Filter nếu bạn đã cấu hình !IsDeleted)
            };
        }
    }
}
