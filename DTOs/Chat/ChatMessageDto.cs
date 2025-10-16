namespace DTOs.Chat;

/// <summary>
/// Represents a chat message.
/// </summary>
public sealed record ChatMessageDto(
    string Id,
    string Channel,
    Guid FromUserId,
    Guid? ToUserId,
    Guid? RoomId,
    string Text,
    DateTime SentAt
);
