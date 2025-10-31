using BusinessObjects.Common.Results;

namespace Services.Interfaces;

public interface IPlatformAccountService
{
    /// <summary>
    /// Ensures the platform user exists and returns its identifier.
    /// </summary>
    Task<Result<Guid>> GetOrCreatePlatformUserIdAsync(CancellationToken ct = default);
}
