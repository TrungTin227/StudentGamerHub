namespace Repositories.Interfaces;

/// <summary>
/// Query interface for FriendLink entity - Dashboard feature
/// </summary>
public interface IFriendLinkQuerRepository
{
    /// <summary>
    /// Get list of accepted friend IDs for current user
    /// Returns the "other person" ID from FriendLink where Status=Accepted
    /// (If currentUserId is Sender, return RecipientId; if Recipient, return SenderId)
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAcceptedFriendIdsAsync(
        Guid currentUserId, 
        CancellationToken ct = default);
}
