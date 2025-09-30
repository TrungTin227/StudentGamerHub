namespace Services.Common.Abstractions;

public interface IReadOnlyService<TReadDto, TKey>
{
    Task<Result<TReadDto>> GetByIdAsync(TKey id, CancellationToken ct = default);
    Task<Result<PagedResult<TReadDto>>> GetPagedAsync(PageRequest req, CancellationToken ct = default);
    Task<Result<bool>> ExistsAsync(TKey id, CancellationToken ct = default);
}
