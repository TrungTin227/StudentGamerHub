using Microsoft.EntityFrameworkCore;

namespace Repositories.Implements;

/// <summary>
/// Community command repository implementation.
/// </summary>
public sealed class CommunityCommandRepository : ICommunityCommandRepository
{
    private readonly AppDbContext _context;

    public CommunityCommandRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task CreateAsync(Community community, CancellationToken ct = default)
    {
        await _context.Communities.AddAsync(community, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(Community community, CancellationToken ct = default)
    {
        _context.Communities.Update(community);
        return Task.CompletedTask;
    }

    public async Task SoftDeleteAsync(Guid communityId, Guid deletedBy, CancellationToken ct = default)
    {
        var community = await _context.Communities
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == communityId, ct)
            .ConfigureAwait(false);

        if (community is null)
            return;

        community.IsDeleted = true;
        community.DeletedAtUtc = DateTime.UtcNow;
        community.DeletedBy = deletedBy;
        community.UpdatedAtUtc = DateTime.UtcNow;
        community.UpdatedBy = deletedBy;

        _context.Communities.Update(community);
    }
}
