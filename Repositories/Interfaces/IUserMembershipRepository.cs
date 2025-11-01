namespace Repositories.Interfaces;

public interface IUserMembershipRepository
{
    Task<UserMembership?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserMembership?> GetActiveAsync(Guid userId, DateTime utcNow, CancellationToken ct = default);
    Task<UserMembership?> GetForUpdateAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(UserMembership membership, CancellationToken ct = default);
    Task UpdateAsync(UserMembership membership);
    Task<int?> DecrementQuotaIfAvailableAsync(Guid membershipId, Guid actorId, DateTime utcNow, CancellationToken ct = default);
}
