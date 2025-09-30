namespace Services.Common.Abstractions;

public interface ICrudService<TCreateDto, TUpdateDto, TReadDto, TKey>
{
    // Read
    Task<Result<TReadDto>> GetByIdAsync(TKey id, CancellationToken ct = default);
    Task<Result<PagedResult<TReadDto>>> ListAsync(PageRequest paging, CancellationToken ct = default);

    // Single (wrapper)
    Task<Result<TReadDto>> CreateAsync(TCreateDto dto, CancellationToken ct = default);
    Task<Result<TReadDto>> UpdateAsync(TKey id, TUpdateDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(TKey id, CancellationToken ct = default);
    Task<Result> SoftDeleteAsync(TKey id, CancellationToken ct = default);
    Task<Result> RestoreAsync(TKey id, CancellationToken ct = default);

    // Batch (core) — DÙNG KIỂU Ở BO
    Task<Result<BatchResult<TKey, TReadDto>>> CreateManyAsync(IEnumerable<TCreateDto> dtos, bool transactional = true, CancellationToken ct = default);
    Task<Result<BatchResult<TKey, TReadDto>>> UpdateManyAsync(IEnumerable<UpdateItem<TKey, TUpdateDto>> items, bool transactional = true, CancellationToken ct = default);
    Task<Result<BatchOutcome<TKey>>> DeleteManyAsync(IEnumerable<TKey> ids, bool transactional = true, CancellationToken ct = default);
    Task<Result<BatchOutcome<TKey>>> SoftDeleteManyAsync(IEnumerable<TKey> ids, bool transactional = true, CancellationToken ct = default);
    Task<Result<BatchOutcome<TKey>>> RestoreManyAsync(IEnumerable<TKey> ids, bool transactional = true, CancellationToken ct = default);
}


