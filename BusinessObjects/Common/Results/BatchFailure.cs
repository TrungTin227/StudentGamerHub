using System.Text;

namespace BusinessObjects.Common.Results
{
    /// <summary>Lỗi theo từng phần tử trong batch.</summary>
    public sealed record BatchFailure<TKey>(TKey Id, Error Error);

    /// <summary>Batch CÓ trả item (vd: create/update nhiều bản ghi).</summary>
    public sealed record BatchResult<TKey, TItem>(
        IReadOnlyList<TItem> Items,
        IReadOnlyList<BatchFailure<TKey>> Failures)
    {
        public bool AllSucceeded => Failures.Count == 0;

        public static BatchResult<TKey, TItem> Success(IReadOnlyList<TItem> items)
            => new(items, Array.Empty<BatchFailure<TKey>>());

        public static BatchResult<TKey, TItem> Partial(
            IReadOnlyList<TItem> items,
            IReadOnlyList<BatchFailure<TKey>> fails)
            => new(items, fails);
    }

    /// <summary>Batch KHÔNG trả item (vd: delete/soft-delete/restore).</summary>
    public sealed record BatchOutcome<TKey>(
        int Succeeded,
        IReadOnlyList<BatchFailure<TKey>> Failures)
    {
        public bool AllSucceeded => Failures.Count == 0;

        public static BatchOutcome<TKey> Success(int count)
            => new(count, Array.Empty<BatchFailure<TKey>>());
    }

    // Error helpers cho batch
    public static class BatchErrors
    {
        public static Error ExpectedExactlyOne(int count) =>
            new(Error.Codes.Unexpected, $"Expected exactly 1 affected item, got {count}.");

        public static Error HasFailures<TKey>(int count, IReadOnlyList<BatchFailure<TKey>> fails)
        {
            var sb = new StringBuilder();
            sb.Append($"Batch had {count} failure(s). ");
            foreach (var f in fails.Take(3))
                sb.Append($"[{f.Id}] {f.Error.Code}: {f.Error.Message}. ");
            return new(Error.Codes.Unexpected, sb.ToString().Trim());
        }
    }
}
