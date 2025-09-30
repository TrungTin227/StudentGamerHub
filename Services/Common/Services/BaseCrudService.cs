namespace Services.Common.Services
{
    /// <summary>
    /// Base CRUD theo hướng “batch-core, single-wrapper”.
    /// - Lớp con chỉ cần implement các phương thức batch + đọc (read).
    /// - Các phương thức đơn (create/update/delete/...) chỉ là wrapper gọi batch tương ứng.
    /// </summary>
    public abstract class BaseCrudService<TCreateDto, TUpdateDto, TReadDto, TKey>
        : ICrudService<TCreateDto, TUpdateDto, TReadDto, TKey>
    {

        /// <summary>Lấy chi tiết theo khóa chính.</summary>
        public abstract Task<Result<TReadDto>> GetByIdAsync(TKey id, CancellationToken ct = default);

        /// <summary>Truy vấn phân trang.</summary>
        public abstract Task<Result<PagedResult<TReadDto>>> ListAsync(PageRequest paging, CancellationToken ct = default);


        /// <summary>Tạo nhiều bản ghi.</summary>
        public abstract Task<Result<BatchResult<TKey, TReadDto>>> CreateManyAsync(
            IEnumerable<TCreateDto> dtos,
            bool transactional = true,
            CancellationToken ct = default);

        /// <summary>Cập nhật nhiều bản ghi.</summary>
        public abstract Task<Result<BatchResult<TKey, TReadDto>>> UpdateManyAsync(
            IEnumerable<UpdateItem<TKey, TUpdateDto>> items,
            bool transactional = true,
            CancellationToken ct = default);

        /// <summary>Xóa cứng nhiều bản ghi.</summary>
        public abstract Task<Result<BatchOutcome<TKey>>> DeleteManyAsync(
            IEnumerable<TKey> ids,
            bool transactional = true,
            CancellationToken ct = default);

        /// <summary>Soft-delete nhiều bản ghi.</summary>
        public abstract Task<Result<BatchOutcome<TKey>>> SoftDeleteManyAsync(
            IEnumerable<TKey> ids,
            bool transactional = true,
            CancellationToken ct = default);

        /// <summary>Khôi phục nhiều bản ghi đã soft-delete.</summary>
        public abstract Task<Result<BatchOutcome<TKey>>> RestoreManyAsync(
            IEnumerable<TKey> ids,
            bool transactional = true,
            CancellationToken ct = default);


        /// <summary>Tạo một bản ghi (wrapper của <see cref="CreateManyAsync"/>).</summary>
        public virtual async Task<Result<TReadDto>> CreateAsync(TCreateDto dto, CancellationToken ct = default)
        {
            var batch = await CreateManyAsync(new[] { dto }, transactional: true, ct);
            return batch.ToSingleItem();
        }

        /// <summary>Cập nhật một bản ghi (wrapper của <see cref="UpdateManyAsync"/>).</summary>
        public virtual async Task<Result<TReadDto>> UpdateAsync(TKey id, TUpdateDto dto, CancellationToken ct = default)
        {
            var items = new[] { new UpdateItem<TKey, TUpdateDto>(id, dto) };
            var batch = await UpdateManyAsync(items, transactional: true, ct);
            return batch.ToSingleItem();
        }

        /// <summary>Xóa cứng một bản ghi (wrapper của <see cref="DeleteManyAsync"/>).</summary>
        public virtual async Task<Result> DeleteAsync(TKey id, CancellationToken ct = default)
        {
            var batch = await DeleteManyAsync(new[] { id }, transactional: true, ct);
            return batch.ToSingleVoid(expected: 1);
        }

        /// <summary>Soft-delete một bản ghi (wrapper của <see cref="SoftDeleteManyAsync"/>).</summary>
        public virtual async Task<Result> SoftDeleteAsync(TKey id, CancellationToken ct = default)
        {
            var batch = await SoftDeleteManyAsync(new[] { id }, transactional: true, ct);
            return batch.ToSingleVoid(expected: 1);
        }

        /// <summary>Khôi phục một bản ghi (wrapper của <see cref="RestoreManyAsync"/>).</summary>
        public virtual async Task<Result> RestoreAsync(TKey id, CancellationToken ct = default)
        {
            var batch = await RestoreManyAsync(new[] { id }, transactional: true, ct);
            return batch.ToSingleVoid(expected: 1);
        }

    }
}
