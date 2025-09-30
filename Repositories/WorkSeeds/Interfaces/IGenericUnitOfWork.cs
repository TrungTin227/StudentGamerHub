using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using DbIsolationLevel = System.Data.IsolationLevel;   

namespace Repositories.WorkSeeds.Interfaces
{
    public interface IGenericUnitOfWork : IAsyncDisposable, IDisposable
    {
        DatabaseFacade Database { get; }   // để ExecutionStrategy dùng

        IGenericRepository<TEntity, TKey> GetRepository<TEntity, TKey>()
            where TEntity : class;

        bool HasActiveTransaction { get; }

        Task<IDbContextTransaction> BeginTransactionAsync(
            DbIsolationLevel isolationLevel = DbIsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default);

        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
