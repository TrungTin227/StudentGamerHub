using Microsoft.EntityFrameworkCore;
using DbIsolationLevel = System.Data.IsolationLevel;
using TxIsolationLevel = System.Transactions.IsolationLevel;

namespace Repositories.WorkSeeds.Extensions
{
    public static class TransactionExtensions
    {
        private static async Task<TResult> RunInExecutionStrategyAsync<TResult>(
            this IGenericUnitOfWork uow,
            Func<CancellationToken, Task<TResult>> body,
            CancellationToken ct)
        {
            if (uow is null) throw new ArgumentNullException(nameof(uow));
            if (body is null) throw new ArgumentNullException(nameof(body));

            var strategy = uow.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () => await body(ct)).ConfigureAwait(false);
        }

        public static async Task<Result<T>> ExecuteTransactionAsync<T>(
            this IGenericUnitOfWork uow,
            Func<CancellationToken, Task<Result<T>>> operation,
            DbIsolationLevel isolationLevel = DbIsolationLevel.ReadCommitted,
            CancellationToken ct = default)
        {
            if (uow is null) throw new ArgumentNullException(nameof(uow));
            if (operation is null) throw new ArgumentNullException(nameof(operation));

            if (uow.HasActiveTransaction)
                return await operation(ct).ConfigureAwait(false);

            return await uow.RunInExecutionStrategyAsync<Result<T>>(async innerCt =>
            {
                try
                {
                    await uow.BeginTransactionAsync(isolationLevel, innerCt).ConfigureAwait(false);

                    var result = await operation(innerCt).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        try
                        {
                            await uow.CommitTransactionAsync(innerCt).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            await SafeRollbackAsync(uow, CancellationToken.None).ConfigureAwait(false);
                            throw;
                        }
                        catch (Exception commitEx)
                        {
                            await SafeRollbackAsync(uow, innerCt).ConfigureAwait(false);
                            return Result<T>.Failure(new Error(Error.Codes.Unexpected, $"Commit failed: {commitEx.Message}"));
                        }
                    }
                    else
                    {
                        await SafeRollbackAsync(uow, innerCt).ConfigureAwait(false);
                    }

                    return result;
                }
                catch (OperationCanceledException)
                {
                    await SafeRollbackAsync(uow, CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
                catch (Exception ex)
                {
                    await SafeRollbackAsync(uow, ct).ConfigureAwait(false);
                    return Result<T>.Failure(new Error(Error.Codes.Unexpected, $"Transaction failed: {ex.Message}"));
                }
            }, ct).ConfigureAwait(false);
        }

        public static async Task<Result> ExecuteTransactionAsync(
            this IGenericUnitOfWork uow,
            Func<CancellationToken, Task<Result>> operation,
            DbIsolationLevel isolationLevel = DbIsolationLevel.ReadCommitted,
            CancellationToken ct = default)
        {
            if (uow is null) throw new ArgumentNullException(nameof(uow));
            if (operation is null) throw new ArgumentNullException(nameof(operation));

            if (uow.HasActiveTransaction)
                return await operation(ct).ConfigureAwait(false);

            return await uow.RunInExecutionStrategyAsync<Result>(async innerCt =>
            {
                try
                {
                    await uow.BeginTransactionAsync(isolationLevel, innerCt).ConfigureAwait(false);

                    var result = await operation(innerCt).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        try
                        {
                            await uow.CommitTransactionAsync(innerCt).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            await SafeRollbackAsync(uow, CancellationToken.None).ConfigureAwait(false);
                            throw;
                        }
                        catch (Exception commitEx)
                        {
                            await SafeRollbackAsync(uow, innerCt).ConfigureAwait(false);
                            return Result.Failure(new Error(Error.Codes.Unexpected, $"Commit failed: {commitEx.Message}"));
                        }
                    }
                    else
                    {
                        await SafeRollbackAsync(uow, innerCt).ConfigureAwait(false);
                    }

                    return result;
                }
                catch (OperationCanceledException)
                {
                    await SafeRollbackAsync(uow, CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
                catch (Exception ex)
                {
                    await SafeRollbackAsync(uow, ct).ConfigureAwait(false);
                    return Result.Failure(new Error(Error.Codes.Unexpected, $"Transaction failed: {ex.Message}"));
                }
            }, ct).ConfigureAwait(false);
        }

        public static Task<Result<T>> ExecuteTransactionAsync<T>(
            this IGenericUnitOfWork uow,
            Func<CancellationToken, Task<T>> operation,
            DbIsolationLevel isolationLevel = DbIsolationLevel.ReadCommitted,
            CancellationToken ct = default)
        {
            if (operation is null) throw new ArgumentNullException(nameof(operation));

            return uow.ExecuteTransactionAsync<T>(async innerCt =>
            {
                try
                {
                    var value = await operation(innerCt).ConfigureAwait(false);
                    return Result<T>.Success(value);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    return Result<T>.Failure(new Error(Error.Codes.Unexpected, ex.Message));
                }
            }, isolationLevel, ct);
        }

        public static Task<Result> ExecuteTransactionAsync(
            this IGenericUnitOfWork uow,
            Func<CancellationToken, Task> operation,
            DbIsolationLevel isolationLevel = DbIsolationLevel.ReadCommitted,
            CancellationToken ct = default)
        {
            if (operation is null) throw new ArgumentNullException(nameof(operation));

            return uow.ExecuteTransactionAsync(async innerCt =>
            {
                try
                {
                    await operation(innerCt).ConfigureAwait(false);
                    return Result.Success();
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    return Result.Failure(new Error(Error.Codes.Unexpected, ex.Message));
                }
            }, isolationLevel, ct);
        }

        public static async Task<Result<T>> ExecuteTransactionScopeAsync<T>(
            this IGenericUnitOfWork uow,
            Func<CancellationToken, Task<Result<T>>> operation,
            DbIsolationLevel isolationLevel = DbIsolationLevel.ReadCommitted,
            System.Transactions.TransactionScopeOption scopeOption = System.Transactions.TransactionScopeOption.Required,
            TimeSpan? timeout = null,
            CancellationToken ct = default)
        {
            if (uow is null) throw new ArgumentNullException(nameof(uow));
            if (operation is null) throw new ArgumentNullException(nameof(operation));

            if (uow.HasActiveTransaction)
                return await operation(ct).ConfigureAwait(false);

            return await uow.RunInExecutionStrategyAsync<Result<T>>(async innerCt =>
            {
                var txOptions = new System.Transactions.TransactionOptions
                {
                    IsolationLevel = ToSysTxIsolation(isolationLevel),
                    Timeout = timeout ?? System.Transactions.TransactionManager.DefaultTimeout
                };

                using var scope = new System.Transactions.TransactionScope(
                    scopeOption,
                    txOptions,
                    System.Transactions.TransactionScopeAsyncFlowOption.Enabled);

                try
                {
                    var result = await operation(innerCt).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        scope.Complete();
                        return result;
                    }
                    return result;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    return Result<T>.Failure(new Error(Error.Codes.Unexpected, $"TransactionScope failed: {ex.Message}"));
                }
            }, ct).ConfigureAwait(false);
        }

        private static async Task SafeRollbackAsync(IGenericUnitOfWork uow, CancellationToken ct)
        {
            try { await uow.RollbackTransactionAsync(ct).ConfigureAwait(false); }
            catch { /* log if needed */ }
        }

        private static System.Transactions.IsolationLevel ToSysTxIsolation(DbIsolationLevel efIso)
            => efIso switch
            {
                DbIsolationLevel.Chaos => TxIsolationLevel.Chaos,
                DbIsolationLevel.ReadUncommitted => TxIsolationLevel.ReadUncommitted,
                DbIsolationLevel.ReadCommitted => TxIsolationLevel.ReadCommitted,
                DbIsolationLevel.RepeatableRead => TxIsolationLevel.RepeatableRead,
                DbIsolationLevel.Serializable => TxIsolationLevel.Serializable,
                DbIsolationLevel.Snapshot => TxIsolationLevel.Snapshot,
                _ => TxIsolationLevel.ReadCommitted
            };
    }
}
