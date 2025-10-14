namespace DTOs.Chat;

/// <summary>
/// Request to report abuse in chat.
/// </summary>
public sealed record AbuseReportRequest(
    string Channel,
    string MessageId,
    string? Text,
    Guid? OffenderUserId,
    AbuseSnapshotDto? Snapshot
);

/// <summary>
/// Snapshot of the abusive message context.
/// </summary>
public sealed record AbuseSnapshotDto(
    Guid FromUserId,
    Guid? ToUserId,
    Guid? RoomId,
    string Message
);
