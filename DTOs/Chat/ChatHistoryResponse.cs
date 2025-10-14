namespace DTOs.Chat;

/// <summary>
/// Response containing chat history for a channel.
/// </summary>
public sealed record ChatHistoryResponse(
    string Channel,
    IReadOnlyList<ChatMessageDto> Items,
    string? NextAfterId
);
