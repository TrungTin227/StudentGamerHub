namespace BusinessObjects.Common.Results
{
    public static class BatchResultExtensions
    {
        public static Result<TItem> ToSingleItem<TKey, TItem>(
            this Result<BatchResult<TKey, TItem>> batch)
        {
            if (batch.IsFailure) return Result<TItem>.Failure(batch.Error);

            var b = batch.Value!;
            if (b.Failures.Count > 0)
                return Result<TItem>.Failure(BatchErrors.HasFailures(b.Failures.Count, b.Failures));

            if (b.Items.Count != 1)
                return Result<TItem>.Failure(BatchErrors.ExpectedExactlyOne(b.Items.Count));

            return Result<TItem>.Success(b.Items[0]);
        }

        public static Result ToSingleVoid<TKey>(
            this Result<BatchOutcome<TKey>> batch,
            int expected = 1)
        {
            if (batch.IsFailure) return Result.Failure(batch.Error);

            var b = batch.Value!;
            if (b.Failures.Count > 0)
                return Result.Failure(BatchErrors.HasFailures(b.Failures.Count, b.Failures));

            if (b.Succeeded != expected)
                return Result.Failure(BatchErrors.ExpectedExactlyOne(b.Succeeded));

            return Result.Success();
        }

        // Convert BatchResult to Result<Dictionary<TKey, TItem>> for fast lookups
        public static Result<IReadOnlyDictionary<TKey, TItem>> ToDictionary<TKey, TItem>(
            this Result<BatchResult<TKey, TItem>> batch,
            Func<TItem, TKey> keySelector,
            IEqualityComparer<TKey>? comparer = null)
            where TKey : notnull
        {
            if (batch.IsFailure) return Result<IReadOnlyDictionary<TKey, TItem>>.Failure(batch.Error);

            var b = batch.Value!;
            if (b.Failures.Count > 0)
                return Result<IReadOnlyDictionary<TKey, TItem>>.Failure(BatchErrors.HasFailures(b.Failures.Count, b.Failures));

            var dict = (comparer is null)
                ? b.Items.ToDictionary(keySelector)
                : b.Items.ToDictionary(keySelector, comparer);

            return Result<IReadOnlyDictionary<TKey, TItem>>.Success(dict);
        }

        // Null-safety: ensure non-null items for reference types
        public static Result<BatchResult<TKey, TItem>> EnsureNoNullItems<TKey, TItem>(
            this Result<BatchResult<TKey, TItem>> batch,
            Error ifNull)
        {
            if (batch.IsFailure) return batch;
            var b = batch.Value!;
            if (b.Items.Any(i => i is null))
            {
                return Result<BatchResult<TKey, TItem>>.Failure(ifNull);
            }
            return batch;
        }
    }
}
