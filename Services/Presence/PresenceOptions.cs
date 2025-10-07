namespace Services.Presence;

public sealed class PresenceOptions
{
    public const string SectionName = "Presence";

    public int TtlSeconds { get; init; } = 60;

    public int HeartbeatSeconds { get; init; } = 30;
}
