namespace Services.Presence;

public sealed class PresenceOptions
{
    public const string SectionName = "Presence";

    public int TtlSeconds { get; init; } = 60;

    public int HeartbeatSeconds { get; init; } = 30;

    public string KeyPrefix { get; init; } = "sg";

    public int GraceSeconds { get; init; } = 5;

    public int MaxBatchSize { get; init; } = 200;

    public int DefaultPageSize { get; init; } = 100;

    public int MaxPageSize { get; init; } = 500;
}
