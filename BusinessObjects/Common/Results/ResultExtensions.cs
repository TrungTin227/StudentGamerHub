namespace BusinessObjects.Common.Results
{
    /// <summary>
    /// Extensions tối giản & thống nhất để chain Result/Result{T}:
    /// Bind / Ensure / TapAsync / Try / ToResult (+ wrappers cho Task{Result}).
    /// </summary>
    public static class ResultExtensions
    {
        // ======================================================================
        // BIND — chuyển tiếp khi Success
        // ======================================================================

        // Result<TIn> -> Result<TOut>
        public static Result<TOut> Bind<TIn, TOut>(
            this Result<TIn> result,
            Func<TIn, Result<TOut>> next)
        {
            if (result.IsSuccess)
            {
                return next(result.Value!);
            }
            return Result<TOut>.Failure(result.Error);
        }

        // Result<TIn> -> Task<Result<TOut>>
        public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
            this Result<TIn> result,
            Func<TIn, Task<Result<TOut>>> nextAsync)
        {
            if (result.IsSuccess)
            {
                return await nextAsync(result.Value!).ConfigureAwait(false);
            }
            return Result<TOut>.Failure(result.Error);
        }

        // Task<Result<TIn>> -> Task<Result<TOut>>
        public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
            this Task<Result<TIn>> resultTask,
            Func<TIn, Task<Result<TOut>>> nextAsync)
        {
            var result = await resultTask.ConfigureAwait(false);
            return await result.BindAsync(nextAsync).ConfigureAwait(false);
        }

        // ---------- Đích non-generic (Result) ----------
        // Result<TIn> -> Result
        public static Result Bind<TIn>(
            this Result<TIn> result,
            Func<TIn, Result> next)
        {
            if (result.IsSuccess)
            {
                return next(result.Value!);
            }
            return Result.Failure(result.Error);
        }

        // Result<TIn> -> Task<Result>
        public static async Task<Result> BindAsync<TIn>(
            this Result<TIn> result,
            Func<TIn, Task<Result>> nextAsync)
        {
            if (result.IsSuccess)
            {
                return await nextAsync(result.Value!).ConfigureAwait(false);
            }
            return Result.Failure(result.Error);
        }

        // Task<Result<TIn>> -> Task<Result>
        public static async Task<Result> BindAsync<TIn>(
            this Task<Result<TIn>> resultTask,
            Func<TIn, Task<Result>> nextAsync)
        {
            var result = await resultTask.ConfigureAwait(false);
            return await result.BindAsync(nextAsync).ConfigureAwait(false);
        }

        // ======================================================================
        // ENSURE — business guard
        // ======================================================================

        // Result<T> + predicate sync
        public static Result<T> Ensure<T>(
            this Result<T> result,
            Func<T, bool> predicate,
            Error ifFalse)
        {
            if (result.IsSuccess)
            {
                if (!predicate(result.Value!))
                {
                    return Result<T>.Failure(ifFalse);
                }
            }
            return result;
        }

        // Result<T> + predicate async
        public static async Task<Result<T>> EnsureAsync<T>(
            this Result<T> result,
            Func<T, Task<bool>> predicateAsync,
            Error ifFalse)
        {
            if (!result.IsSuccess)
            {
                return result;
            }

            bool ok = await predicateAsync(result.Value!).ConfigureAwait(false);
            if (!ok)
            {
                return Result<T>.Failure(ifFalse);
            }
            return result;
        }

        // ---- WRAPPERS cho Task<Result<T>> (rất quan trọng) ----

        // Task<Result<T>> + predicate sync
        public static async Task<Result<T>> Ensure<T>(
            this Task<Result<T>> resultTask,
            Func<T, bool> predicate,
            Error ifFalse)
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.Ensure(predicate, ifFalse);
        }

        // Task<Result<T>> + predicate async
        public static async Task<Result<T>> EnsureAsync<T>(
            this Task<Result<T>> resultTask,
            Func<T, Task<bool>> predicateAsync,
            Error ifFalse)
        {
            var result = await resultTask.ConfigureAwait(false);
            return await result.EnsureAsync(predicateAsync, ifFalse).ConfigureAwait(false);
        }

        // ======================================================================
        // TAP — side-effect khi Success, không đổi kết quả
        // ======================================================================

        // Result (non-generic)
        public static async Task<Result> TapAsync(
            this Result result,
            Func<Task> onSuccessAsync)
        {
            if (result.IsSuccess)
            {
                await onSuccessAsync().ConfigureAwait(false);
            }
            return result;
        }

        // Wrapper cho Task<Result>
        public static async Task<Result> TapAsync(
            this Task<Result> resultTask,
            Func<Task> onSuccessAsync)
        {
            var result = await resultTask.ConfigureAwait(false);
            return await result.TapAsync(onSuccessAsync).ConfigureAwait(false);
        }

        // Result<T>
        public static async Task<Result<T>> TapAsync<T>(
            this Result<T> result,
            Func<T, Task> onSuccessAsync)
        {
            if (result.IsSuccess)
            {
                await onSuccessAsync(result.Value!).ConfigureAwait(false);
            }
            return result;
        }

        // Wrapper cho Task<Result<T>>
        public static async Task<Result<T>> TapAsync<T>(
            this Task<Result<T>> resultTask,
            Func<T, Task> onSuccessAsync)
        {
            var result = await resultTask.ConfigureAwait(false);
            return await result.TapAsync(onSuccessAsync).ConfigureAwait(false);
        }

        // ======================================================================
        // TRY — bọc exception về Result
        // ======================================================================

        public static Result<T> Try<T>(
            Func<T> func,
            Func<Exception, Error>? errorFactory = null)
        {
            try
            {
                var value = func();
                return Result<T>.Success(value);
            }
            catch (Exception ex)
            {
                var error = errorFactory is not null ? errorFactory(ex) : CreateUnexpectedError(ex);
                return Result<T>.Failure(error);
            }
        }

        public static async Task<Result<T>> TryAsync<T>(
            Func<Task<T>> funcAsync,
            Func<Exception, Error>? errorFactory = null)
        {
            try
            {
                var value = await funcAsync().ConfigureAwait(false);
                return Result<T>.Success(value);
            }
            catch (Exception ex)
            {
                var error = errorFactory is not null ? errorFactory(ex) : CreateUnexpectedError(ex);
                return Result<T>.Failure(error);
            }
        }

        // ======================================================================
        // MISC
        // ======================================================================

        public static Result ToResult<T>(this Result<T> result)
        {
            if (result.IsSuccess)
            {
                return Result.Success();
            }
            return Result.Failure(result.Error);
        }

        private static Error CreateUnexpectedError(Exception ex)
        {
            return new Error(Error.Codes.Unexpected, ex.Message);
        }
    }
}
