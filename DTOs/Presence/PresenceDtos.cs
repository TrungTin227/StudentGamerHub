namespace DTOs.Presence;

public sealed record PresenceSnapshotItem(
    Guid UserId,
    bool IsOnline,
    DateTimeOffset? LastSeenUtc,
    int? TtlRemainingSeconds);

public sealed record PresenceBatchRequest(IReadOnlyCollection<Guid> UserIds);

public sealed record PresenceBatchResponse(IReadOnlyCollection<PresenceSnapshotItem> Items);

public sealed record PresenceOnlineQuery(int? PageSize = null, string? Cursor = null);

public sealed record PresenceOnlineResponse(
    IReadOnlyCollection<PresenceSnapshotItem> Items,
    string? NextCursor);

public sealed record PresenceSummaryRequest(IReadOnlyCollection<Guid>? UserIds);

public sealed record PresenceSummaryResponse(int Online, int? Offline, int Total, string Scope);
