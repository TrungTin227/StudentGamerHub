using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using DbIsolationLevel = System.Data.IsolationLevel;
using TxIsolationLevel = System.Transactions.IsolationLevel;

namespace Repositories.WorkSeeds.Implements
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private readonly IRepositoryFactory _factory;
        private IDbContextTransaction? _currentTx;

        public UnitOfWork(AppDbContext context, IRepositoryFactory factory)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }
        public DatabaseFacade Database => _context.Database; 

        public bool HasActiveTransaction => _currentTx is not null;

        public IGenericRepository<TEntity, TKey> GetRepository<TEntity, TKey>()
            where TEntity : class
            => _factory.GetRepository<TEntity, TKey>();

        // Overload chính cho EF Core (System.Data)
        public async Task<IDbContextTransaction> BeginTransactionAsync(
            DbIsolationLevel isolationLevel = DbIsolationLevel.ReadCommitted,
            CancellationToken ct = default)
        {
            EnsureNoActiveTransaction();
            _currentTx = await _context.Database.BeginTransactionAsync(isolationLevel, ct);
            return _currentTx;
        }

        // Overload tiện cho TransactionScope (System.Transactions)
        public Task<IDbContextTransaction> BeginTransactionAsync(
            TxIsolationLevel isolationLevel = TxIsolationLevel.ReadCommitted,
            CancellationToken ct = default)
            => BeginTransactionAsync(MapIsolationLevel(isolationLevel), ct);

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);

        public Task CommitTransactionAsync(CancellationToken ct = default)
            => EndAsync((tx, c) => tx.CommitAsync(c), ct);

        public Task RollbackTransactionAsync(CancellationToken ct = default)
            => EndAsync((tx, c) => tx.RollbackAsync(c), ct);

        public void Dispose()
        {
            _currentTx?.Dispose();
            _currentTx = null;
        }

        public async ValueTask DisposeAsync()
        {
            if (_currentTx is not null)
            {
                await _currentTx.DisposeAsync();
                _currentTx = null;
            }
        }

        public void ClearChangeTracker()
        {
            _context.ChangeTracker.Clear();
        }

        // ---- helpers -------------------------------------------------

        private void EnsureNoActiveTransaction()
        {
            if (_currentTx is not null)
                throw new InvalidOperationException("A transaction is already active.");
        }

        private static DbIsolationLevel MapIsolationLevel(TxIsolationLevel level) => level switch
        {
            TxIsolationLevel.ReadUncommitted => DbIsolationLevel.ReadUncommitted,
            TxIsolationLevel.ReadCommitted   => DbIsolationLevel.ReadCommitted,
            TxIsolationLevel.RepeatableRead  => DbIsolationLevel.RepeatableRead,
            TxIsolationLevel.Serializable    => DbIsolationLevel.Serializable,
            TxIsolationLevel.Snapshot        => DbIsolationLevel.Snapshot,
            TxIsolationLevel.Chaos           => DbIsolationLevel.Chaos,
            _                                => DbIsolationLevel.Unspecified
        };

        private async Task EndAsync(
            Func<IDbContextTransaction, CancellationToken, Task> action,
            CancellationToken ct)
        {
            if (_currentTx is null) return;

            try
            {
                await action(_currentTx, ct);
            }
            finally
            {
                await _currentTx.DisposeAsync();
                _currentTx = null;
            }
        }
    }
}
