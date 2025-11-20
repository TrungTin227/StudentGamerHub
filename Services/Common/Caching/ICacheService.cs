namespace Services.Common.Caching;

/// <summary>
/// Abstraction for distributed caching with typed support.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Get cached value by key.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Set cached value with expiration.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Remove cached value.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Remove multiple cached values by pattern.
    /// </summary>
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);

    /// <summary>
    /// Get or set cache with factory function.
    /// </summary>
    Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken ct = default) where T : class;
}
