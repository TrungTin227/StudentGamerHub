namespace BusinessObjects.Common.Results
{
    public class Result
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public Error Error { get; }

        protected Result(bool isSuccess, Error error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public static Result Success() => new(true, Error.None);
        public static Result Failure(Error error) => new(false, error);

        public static Result Combine(params Result[] results)
        {
            foreach (var r in results) if (r.IsFailure) return r;
            return Success();
        }
    }

    public sealed class Result<T> : Result
    {
        public T? Value { get; }

        private Result(T? value, bool isSuccess, Error error) : base(isSuccess, error) => Value = value;

        public static Result<T> Success(T value) => new(value, true, Error.None);
        public static new Result<T> Failure(Error error) => new(default, false, error);

        // tiện return trực tiếp T (nếu không thích implicit, có thể bỏ để code rõ ràng hơn)
        public static implicit operator Result<T>(T value) => Success(value);

        // helper: null safety
        public static Result<T> FromNullable(T? value, Error ifNull)
            => value is null ? Failure(ifNull) : Success(value);
    }
}
