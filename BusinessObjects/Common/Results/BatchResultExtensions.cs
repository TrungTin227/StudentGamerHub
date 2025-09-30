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
    }
}
