using BusinessObjects.Common.Results;
using DTOs.Presence;

namespace Services.Presence;

public interface IPresenceReader
{
    Task<Result<PresenceOnlineResponse>> GetOnlineAsync(PresenceOnlineQuery query, CancellationToken ct);

    Task<Result<IReadOnlyCollection<PresenceSnapshotItem>>> GetBatchAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    Task<Result<PresenceSummaryResponse>> GetSummaryAsync(PresenceSummaryRequest request, CancellationToken ct);
}
