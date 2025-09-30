using FluentValidation;
using DbIsolationLevel = System.Data.IsolationLevel;
namespace Services.Implementations
{
    public sealed class RoleService
        : BaseCrudService<CreateRoleRequest, UpdateRoleRequest, RoleDto, Guid>, IRoleService
    {
        private readonly IUnitOfWork _uow;
        private readonly IRoleRepository _roles;
        private readonly IValidator<CreateRoleRequest> _createVal;
        private readonly IValidator<UpdateRoleRequest> _updateVal;
        private readonly ICurrentUserService _current;

        public RoleService(
            IUnitOfWork uow,
            IRoleRepository roles,
            IValidator<CreateRoleRequest> createVal,
            IValidator<UpdateRoleRequest> updateVal,
            ICurrentUserService current)
        {
            _uow = uow;
            _roles = roles;
            _createVal = createVal;
            _updateVal = updateVal;
            _current = current;
        }

        // ====================== READ ======================

        public override async Task<Result<RoleDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await _roles.GetByIdAsync(id, asNoTracking: true, ct).ConfigureAwait(false);
            if (entity is null)
                return Result<RoleDto>.Failure(new Error(Error.Codes.NotFound, $"Role '{id}' not found."));
            return Map(entity);
        }

        // CRUD base yêu cầu ListAsync(PageRequest) — dùng SearchPagedAsync với filter mặc định (OnlyActive)
        public override async Task<Result<PagedResult<RoleDto>>> ListAsync(PageRequest paging, CancellationToken ct = default)
        {
            var page = await _roles.SearchPagedAsync(paging, filter: null, ct).ConfigureAwait(false);
            return Result<PagedResult<RoleDto>>.Success(MapPage(page));
        }

        // Overload tiện dụng cho controller UI muốn truyền filter
        public async Task<Result<PagedResult<RoleDto>>> ListAsync(PageRequest paging, RoleFilter? filter, CancellationToken ct = default)
        {
            var page = await _roles.SearchPagedAsync(paging, filter, ct).ConfigureAwait(false);
            return Result<PagedResult<RoleDto>>.Success(MapPage(page));
        }

        public async Task<Result<bool>> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
        {
            // Check trong các role đang active (giống logic cũ includeDeleted:false)
            var exists = await _roles.NameExistsAsync(name, excludeId, DeletedFilter.OnlyActive, ct).ConfigureAwait(false);
            return Result<bool>.Success(exists);
        }

        // ====================== CREATE (BATCH) ======================

        public override async Task<Result<BatchResult<Guid, RoleDto>>> CreateManyAsync(
            IEnumerable<CreateRoleRequest> dtos,
            bool transactional = true,
            CancellationToken ct = default)
        {
            if (!transactional)
            {
                var successes = new List<RoleDto>();
                var failures = new List<BatchFailure<Guid>>();

                foreach (var dto in dtos)
                {
                    var v = await _createVal.ValidateToResultAsync(dto, ct);
                    if (v.IsFailure)
                    {
                        failures.Add(new BatchFailure<Guid>(Guid.Empty, v.Error));
                        continue;
                    }

                    var normalized = Normalize(dto.Name);
                    // Ngăn trùng với role còn active
                    if (await _roles.NormalizedNameExistsAsync(normalized, excludeId: null, DeletedFilter.OnlyActive, ct))
                    {
                        failures.Add(new BatchFailure<Guid>(Guid.Empty,
                            new Error(Error.Codes.Conflict, $"Role '{dto.Name}' already exists.")));
                        continue;
                    }

                    var entity = NewRoleFromCreate(dto, normalized, _current.UserId);
                    await _roles.AddAsync(entity, ct);
                    await _uow.SaveChangesAsync(ct); // save từng item (partial mode)
                    successes.Add(Map(entity));
                }

                return Result<BatchResult<Guid, RoleDto>>.Success(
                    failures.Count == 0
                        ? BatchResult<Guid, RoleDto>.Success(successes)
                        : BatchResult<Guid, RoleDto>.Partial(successes, failures));
            }

            // Transactional: dùng ExecutionStrategy + rollback-all nếu có bất kỳ lỗi
            return await _uow.ExecuteTransactionAsync(async innerCt =>
            {
                var successes = new List<RoleDto>();
                var failures = new List<BatchFailure<Guid>>();

                foreach (var dto in dtos)
                {
                    var v = await _createVal.ValidateToResultAsync(dto, innerCt);
                    if (v.IsFailure)
                    {
                        failures.Add(new BatchFailure<Guid>(Guid.Empty, v.Error));
                        continue;
                    }

                    var normalized = Normalize(dto.Name);
                    if (await _roles.NormalizedNameExistsAsync(normalized, excludeId: null, DeletedFilter.OnlyActive, innerCt))
                    {
                        failures.Add(new BatchFailure<Guid>(Guid.Empty,
                            new Error(Error.Codes.Conflict, $"Role '{dto.Name}' already exists.")));
                        continue;
                    }

                    var entity = NewRoleFromCreate(dto, normalized, _current.UserId);
                    await _roles.AddAsync(entity, innerCt);
                    successes.Add(Map(entity));
                }

                if (failures.Count > 0)
                    return Result<BatchResult<Guid, RoleDto>>.Failure(BatchErrors.HasFailures(failures.Count, failures));

                await _uow.SaveChangesAsync(innerCt);
                return Result<BatchResult<Guid, RoleDto>>.Success(BatchResult<Guid, RoleDto>.Success(successes));
            }, DbIsolationLevel.ReadCommitted, ct);
        }

        // ====================== UPDATE (BATCH) ======================

        public override async Task<Result<BatchResult<Guid, RoleDto>>> UpdateManyAsync(
            IEnumerable<UpdateItem<Guid, UpdateRoleRequest>> items,
            bool transactional = true,
            CancellationToken ct = default)
        {
            if (!transactional)
            {
                var successes = new List<RoleDto>();
                var failures = new List<BatchFailure<Guid>>();

                foreach (var it in items)
                {
                    var v = await _updateVal.ValidateToResultAsync(it.Dto, ct);
                    if (v.IsFailure) { failures.Add(new BatchFailure<Guid>(it.Id, v.Error)); continue; }

                    var entity = await _roles.GetByIdAsync(it.Id, asNoTracking: false, ct);
                    if (entity is null) { failures.Add(NotFound(it.Id)); continue; }

                    var normalized = Normalize(it.Dto.Name);
                    if (await _roles.NormalizedNameExistsAsync(normalized, it.Id, DeletedFilter.OnlyActive, ct))
                    {
                        failures.Add(new BatchFailure<Guid>(it.Id, ConflictName(it.Dto.Name)));
                        continue;
                    }

                    ApplyUpdate(entity, it.Dto, normalized, _current.UserId);
                    await _roles.UpdateAsync(entity, ct);
                    await _uow.SaveChangesAsync(ct);
                    successes.Add(Map(entity));
                }

                return Result<BatchResult<Guid, RoleDto>>.Success(
                    failures.Count == 0
                        ? BatchResult<Guid, RoleDto>.Success(successes)
                        : BatchResult<Guid, RoleDto>.Partial(successes, failures));
            }

            return await _uow.ExecuteTransactionAsync(async innerCt =>
            {
                var successes = new List<RoleDto>();
                var failures = new List<BatchFailure<Guid>>();

                foreach (var it in items)
                {
                    var v = await _updateVal.ValidateToResultAsync(it.Dto, innerCt);
                    if (v.IsFailure) { failures.Add(new BatchFailure<Guid>(it.Id, v.Error)); continue; }

                    var entity = await _roles.GetByIdAsync(it.Id, asNoTracking: false, innerCt);
                    if (entity is null) { failures.Add(NotFound(it.Id)); continue; }

                    var normalized = Normalize(it.Dto.Name);
                    if (await _roles.NormalizedNameExistsAsync(normalized, it.Id, DeletedFilter.OnlyActive, innerCt))
                    {
                        failures.Add(new BatchFailure<Guid>(it.Id, ConflictName(it.Dto.Name)));
                        continue;
                    }

                    ApplyUpdate(entity, it.Dto, normalized, _current.UserId);
                    await _roles.UpdateAsync(entity, innerCt);
                    successes.Add(Map(entity));
                }

                if (failures.Count > 0)
                    return Result<BatchResult<Guid, RoleDto>>.Failure(BatchErrors.HasFailures(failures.Count, failures));

                await _uow.SaveChangesAsync(innerCt);
                return Result<BatchResult<Guid, RoleDto>>.Success(BatchResult<Guid, RoleDto>.Success(successes));
            }, DbIsolationLevel.ReadCommitted, ct);
        }

        // ====================== DELETE / SOFT-DELETE / RESTORE (BATCH) ======================

        public override Task<Result<BatchOutcome<Guid>>> DeleteManyAsync(
            IEnumerable<Guid> ids, bool transactional = true, CancellationToken ct = default)
        {
            if (!transactional) return DeleteManyPartialAsync(ids, ct);

            return _uow.ExecuteTransactionAsync(async innerCt =>
            {
                int ok = 0;
                var failures = new List<BatchFailure<Guid>>();

                foreach (var id in ids)
                {
                    var deleted = await _roles.DeleteAsync(id, innerCt);
                    if (deleted) ok++;
                    else failures.Add(NotFound(id));
                }

                if (failures.Count > 0)
                    return Result<BatchOutcome<Guid>>.Failure(BatchErrors.HasFailures(failures.Count, failures));

                await _uow.SaveChangesAsync(innerCt);
                return Result<BatchOutcome<Guid>>.Success(new BatchOutcome<Guid>(ok, failures));
            }, DbIsolationLevel.ReadCommitted, ct);
        }

        public override Task<Result<BatchOutcome<Guid>>> SoftDeleteManyAsync(
            IEnumerable<Guid> ids, bool transactional = true, CancellationToken ct = default)
        {
            _ = transactional; // luôn transactional

            var uid = _current.UserId;

            return _uow.ExecuteTransactionAsync(async innerCt =>
            {
                int ok = 0;
                var failures = new List<BatchFailure<Guid>>();

                foreach (var id in ids)
                {
                    var deleted = await _roles.SoftDeleteAsync(id, deletedBy: uid, innerCt).ConfigureAwait(false);
                    if (deleted) ok++;
                    else failures.Add(NotFound(id));
                }

                if (failures.Count > 0)
                    return Result<BatchOutcome<Guid>>.Failure(BatchErrors.HasFailures(failures.Count, failures));

                await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
                return Result<BatchOutcome<Guid>>.Success(new BatchOutcome<Guid>(ok, failures));
            }, DbIsolationLevel.ReadCommitted, ct);
        }

        public override Task<Result<BatchOutcome<Guid>>> RestoreManyAsync(
            IEnumerable<Guid> ids, bool transactional = true, CancellationToken ct = default)
        {
            _ = transactional; // luôn transactional

            var uid = _current.UserId;

            return _uow.ExecuteTransactionAsync(async innerCt =>
            {
                int ok = 0;
                var failures = new List<BatchFailure<Guid>>();

                foreach (var id in ids)
                {
                    var restored = await _roles.RestoreAsync(id, restoredBy: uid, innerCt).ConfigureAwait(false);
                    if (restored) ok++;
                    else failures.Add(new BatchFailure<Guid>(id,
                        new Error(Error.Codes.NotFound, $"Role '{id}' not found or not soft-deleted.")));
                }

                if (failures.Count > 0)
                    return Result<BatchOutcome<Guid>>.Failure(BatchErrors.HasFailures(failures.Count, failures));

                await _uow.SaveChangesAsync(innerCt).ConfigureAwait(false);
                return Result<BatchOutcome<Guid>>.Success(new BatchOutcome<Guid>(ok, failures));
            }, DbIsolationLevel.ReadCommitted, ct);
        }

        // ====================== helpers ======================

        private static string Normalize(string? name)
            => (name ?? string.Empty).Trim().ToUpperInvariant();

        private static Role NewRoleFromCreate(CreateRoleRequest dto, string normalized, Guid? createdBy)
            => new Role
            {
                Name = dto.Name!.Trim(),
                NormalizedName = normalized,
                Description = dto.Description?.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = createdBy
            };

        private static void ApplyUpdate(Role entity, UpdateRoleRequest dto, string normalized, Guid? updatedBy)
        {
            entity.Name = dto.Name!.Trim();
            entity.NormalizedName = normalized;
            entity.Description = dto.Description?.Trim();
            entity.UpdatedAtUtc = DateTime.UtcNow;
            entity.UpdatedBy = updatedBy;
        }

        private static BatchFailure<Guid> NotFound(Guid id)
            => new(id, new Error(Error.Codes.NotFound, $"Role '{id}' not found."));

        private static Error ConflictName(string? name)
            => new(Error.Codes.Conflict, $"Role '{name}' already exists.");

        private static RoleDto Map(Role r) => new(
            r.Id, r.Name!, r.Description,
            r.CreatedAtUtc, r.CreatedBy,
            r.UpdatedAtUtc, r.UpdatedBy,
            r.IsDeleted, r.DeletedAtUtc, r.DeletedBy
        );

        private static PagedResult<RoleDto> MapPage(PagedResult<Role> page)
        {
            var items = page.Items.Select(Map).ToList();
            return new PagedResult<RoleDto>(
                Items: items,
                Page: page.Page,
                Size: page.Size,
                TotalCount: page.TotalCount,
                TotalPages: page.TotalPages,
                HasPrevious: page.HasPrevious,
                HasNext: page.HasNext,
                Sort: page.Sort,
                Desc: page.Desc
            );
        }

        private async Task<Result<BatchOutcome<Guid>>> DeleteManyPartialAsync(IEnumerable<Guid> ids, CancellationToken ct)
        {
            int ok = 0;
            var failures = new List<BatchFailure<Guid>>();
            foreach (var id in ids)
            {
                var deleted = await _roles.DeleteAsync(id, ct);
                if (deleted) ok++;
                else failures.Add(NotFound(id));
                await _uow.SaveChangesAsync(ct);
            }
            return Result<BatchOutcome<Guid>>.Success(new BatchOutcome<Guid>(ok, failures));
        }
    }
}
