using DTOs.Chat;

namespace Services.Interfaces;

/// <summary>
/// Service for managing chat history stored in Redis.
/// </summary>
public interface IChatHistoryService
{
    /// <summary>
    /// Appends a direct message to chat history.
    /// </summary>
    /// <param name="fromUserId">User sending the message.</param>
    /// <param name="toUserId">User receiving the message.</param>
    /// <param name="text">Message text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Message ID.</returns>
    Task<string> AppendDmAsync(
        Guid fromUserId,
        Guid toUserId,
        string text,
        CancellationToken ct = default);

    /// <summary>
    /// Appends a room message to chat history.
    /// </summary>
    /// <param name="fromUserId">User sending the message.</param>
    /// <param name="roomId">Room ID.</param>
    /// <param name="text">Message text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Message ID.</returns>
    Task<string> AppendRoomAsync(
        Guid fromUserId,
        Guid roomId,
        string text,
        CancellationToken ct = default);

    /// <summary>
    /// Loads chat history for a channel.
    /// </summary>
    /// <param name="channel">Channel identifier (dm:{min}_{max} or room:{roomId}).</param>
    /// <param name="afterId">Load messages after this ID.</param>
    /// <param name="take">Number of messages to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Chat history response.</returns>
    Task<ChatHistoryResponse> LoadHistoryAsync(
        string channel,
        string? afterId,
        int? take,
        CancellationToken ct = default);
}
