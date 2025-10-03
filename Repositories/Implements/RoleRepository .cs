using Microsoft.EntityFrameworkCore;

namespace Repositories.Implements
{
    public sealed class RoleRepository : GenericRepository<Role, Guid>, IRoleRepository
    {
        public RoleRepository(AppDbContext context) : base(context) { }

        private static string Normalize(string? name)
            => (name ?? string.Empty).Trim().ToUpperInvariant();

        // Áp dụng filter xóa mềm
        private static IQueryable<Role> ApplyDeleted(IQueryable<Role> q, DeletedFilter filter)
            => filter switch
            {
                DeletedFilter.All => q.IgnoreQueryFilters(),
                DeletedFilter.OnlyDeleted => q.IgnoreQueryFilters().Where(r => EF.Property<bool>(r, "IsDeleted")),
                _ => q // OnlyActive: giữ Global Query Filter !IsDeleted
            };

        public async Task<Role?> FindByNameAsync(
            string name,
            DeletedFilter deleted = DeletedFilter.OnlyActive,
            bool asNoTracking = true,
            CancellationToken ct = default)
        {
            var normalized = Normalize(name);
            return await FindByNormalizedNameAsync(normalized, deleted, asNoTracking, ct);
        }

        public async Task<Role?> FindByNormalizedNameAsync(
            string normalizedName,
            DeletedFilter deleted = DeletedFilter.OnlyActive,
            bool asNoTracking = true,
            CancellationToken ct = default)
        {
            IQueryable<Role> q = ApplyDeleted(_dbSet, deleted);
            if (asNoTracking) q = q.AsNoTracking();

            return await q.FirstOrDefaultAsync(r => r.NormalizedName == normalizedName, ct);
        }

        public async Task<bool> NameExistsAsync(
            string name,
            Guid? excludeId = null,
            DeletedFilter deleted = DeletedFilter.OnlyActive,
            CancellationToken ct = default)
            => await NormalizedNameExistsAsync(Normalize(name), excludeId, deleted, ct);

        public async Task<bool> NormalizedNameExistsAsync(
            string normalizedName,
            Guid? excludeId = null,
            DeletedFilter deleted = DeletedFilter.OnlyActive,
            CancellationToken ct = default)
        {
            IQueryable<Role> q = ApplyDeleted(_dbSet, deleted);
            if (excludeId.HasValue) q = q.Where(r => r.Id != excludeId.Value);
            return await q.AnyAsync(r => r.NormalizedName == normalizedName, ct);
        }

        public async Task<Role?> GetWithClaimsAsync(Guid id, bool asNoTracking = true, CancellationToken ct = default)
        {
            IQueryable<Role> q = _dbSet;
            if (asNoTracking) q = q.AsNoTracking();
            return await q
                          .FirstOrDefaultAsync(r => r.Id == id, ct);
        }

        public async Task<Role?> GetWithUsersAsync(Guid id, bool asNoTracking = true, CancellationToken ct = default)
        {
            IQueryable<Role> q = _dbSet;
            if (asNoTracking) q = q.AsNoTracking();
            return await q
                          .FirstOrDefaultAsync(r => r.Id == id, ct);
        }

        public async Task<PagedResult<Role>> SearchPagedAsync(
            PageRequest request,
            RoleFilter? filter = null,
            CancellationToken ct = default)
        {
            IQueryable<Role> q = _dbSet;

            // Deleted
            var del = filter?.Deleted ?? DeletedFilter.OnlyActive;
            q = ApplyDeleted(q, del);

            // Keyword: match Name / Description (PostgreSQL ILIKE)
            if (!string.IsNullOrWhiteSpace(filter?.Keyword))
            {
                var kw = filter!.Keyword.Trim();
                var nkw = Normalize(kw);
                q = q.Where(r =>
                    (r.Name != null && EF.Functions.ILike(r.Name, $"%{kw}%")) ||
                    (r.Description != null && EF.Functions.ILike(r.Description, $"%{kw}%")) ||
                    r.NormalizedName == nkw
                );
            }

            // Created range
            if (filter?.CreatedFromUtc is not null)
                q = q.Where(r => r.CreatedAtUtc >= filter.CreatedFromUtc.Value);
            if (filter?.CreatedToUtc is not null)
                q = q.Where(r => r.CreatedAtUtc <= filter.CreatedToUtc.Value);

            // Sort + paging động
            return await q.ToPagedResultAsync(request, ct);
        }

        // Ghi đè Add/Update để chuẩn hóa Name/NormalizedName + audit
        public override async Task<Role> AddAsync(Role entity, CancellationToken ct = default)
        {
            entity.Name = entity.Name?.Trim();
            entity.NormalizedName = Normalize(entity.Name);
            entity.CreatedAtUtc = entity.CreatedAtUtc == default ? DateTime.UtcNow : entity.CreatedAtUtc;
            return await base.AddAsync(entity, ct);
        }

        public override Task<Role> UpdateAsync(Role entity, CancellationToken ct = default)
        {
            entity.Name = entity.Name?.Trim();
            entity.NormalizedName = Normalize(entity.Name);
            entity.UpdatedAtUtc = DateTime.UtcNow;
            return base.UpdateAsync(entity, ct);
        }
    }
}
