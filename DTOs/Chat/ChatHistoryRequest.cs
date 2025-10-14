namespace DTOs.Chat;

/// <summary>
/// Request to load chat history for a channel.
/// </summary>
public sealed record ChatHistoryRequest(
    string Channel,
    string? AfterId,
    int? Take
);
