using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Repositories.Implements;

public sealed class UserMembershipRepository : IUserMembershipRepository
{
    private readonly AppDbContext _context;

    public UserMembershipRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<UserMembership?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _context.UserMemberships
            .AsNoTracking()
            .Include(m => m.MembershipPlan)
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);

    public Task<UserMembership?> GetActiveAsync(Guid userId, DateTime utcNow, CancellationToken ct = default)
        => _context.UserMemberships
            .AsNoTracking()
            .Include(m => m.MembershipPlan)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.EndDate >= utcNow, ct);

    public Task<UserMembership?> GetForUpdateAsync(Guid userId, CancellationToken ct = default)
        => _context.UserMemberships
            .Include(m => m.MembershipPlan)
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);

    public Task AddAsync(UserMembership membership, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(membership);
        return _context.UserMemberships.AddAsync(membership, ct).AsTask();
    }

    public Task UpdateAsync(UserMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);
        _context.UserMemberships.Update(membership);
        return Task.CompletedTask;
    }

    public async Task<int?> DecrementQuotaIfAvailableAsync(Guid membershipId, Guid actorId, DateTime utcNow, CancellationToken ct = default)
    {
        var database = _context.Database;
        var connection = database.GetDbConnection();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE user_memberships
            SET "RemainingEventQuota" = "RemainingEventQuota" - 1,
                "UpdatedAtUtc" = @updatedAtUtc,
                "UpdatedBy" = @updatedBy
            WHERE "Id" = @membershipId AND "RemainingEventQuota" > 0
            RETURNING "RemainingEventQuota";
            """;
        command.Transaction = database.CurrentTransaction?.GetDbTransaction();

        var membershipParam = command.CreateParameter();
        membershipParam.ParameterName = "@membershipId";
        membershipParam.DbType = DbType.Guid;
        membershipParam.Value = membershipId;
        command.Parameters.Add(membershipParam);

        var updatedAtParam = command.CreateParameter();
        updatedAtParam.ParameterName = "@updatedAtUtc";
        updatedAtParam.DbType = DbType.DateTime;
        updatedAtParam.Value = utcNow;
        command.Parameters.Add(updatedAtParam);

        var updatedByParam = command.CreateParameter();
        updatedByParam.ParameterName = "@updatedBy";
        updatedByParam.DbType = DbType.Guid;
        updatedByParam.Value = actorId;
        command.Parameters.Add(updatedByParam);

        var closeConnection = false;
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            closeConnection = true;
        }

        try
        {
            var scalar = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (scalar is null || scalar == DBNull.Value)
            {
                return null;
            }

            return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
    }
}
