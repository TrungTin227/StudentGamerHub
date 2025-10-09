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

        // Task<Result<TIn>> -> Task<Result<TOut>> (next async)
        public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
            this Task<Result<TIn>> resultTask,
            Func<TIn, Task<Result<TOut>>> nextAsync)
        {
            var result = await resultTask.ConfigureAwait(false);
            return await result.BindAsync(nextAsync).ConfigureAwait(false);
        }

        // Task<Result<TIn>> -> Task<Result<TOut>> (next sync)
        public static async Task<Result<TOut>> Bind<TIn, TOut>(
            this Task<Result<TIn>> resultTask,
            Func<TIn, Result<TOut>> next)
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.IsSuccess ? next(result.Value!) : Result<TOut>.Failure(result.Error);
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

        // Task<Result<TIn>> -> Task<Result> (next async)
        public static async Task<Result> BindAsync<TIn>(
            this Task<Result<TIn>> resultTask,
            Func<TIn, Task<Result>> nextAsync)
        {
            var result = await resultTask.ConfigureAwait(false);
            return await result.BindAsync(nextAsync).ConfigureAwait(false);
        }

        // Task<Result<TIn>> -> Task<Result> (next sync)
        public static async Task<Result> Bind<TIn>(
            this Task<Result<TIn>> resultTask,
            Func<TIn, Result> next)
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.IsSuccess ? next(result.Value!) : Result.Failure(result.Error);
        }

        // ======================================================================
        // MAP — biến đổi giá trị khi Success, giữ nguyên Error
        // ======================================================================

        // Result<TIn> -> Result<TOut>
        public static Result<TOut> Map<TIn, TOut>(
            this Result<TIn> result,
            Func<TIn, TOut> selector)
        {
            return result.IsSuccess
                ? Result<TOut>.Success(selector(result.Value!))
                : Result<TOut>.Failure(result.Error);
        }

        // Result<TIn> -> Task<Result<TOut>> (selector async)
        public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
            this Result<TIn> result,
            Func<TIn, Task<TOut>> selectorAsync)
        {
            if (!result.IsSuccess) return Result<TOut>.Failure(result.Error);
            var value = await selectorAsync(result.Value!).ConfigureAwait(false);
            return Result<TOut>.Success(value);
        }

        // Task<Result<TIn>> -> Task<Result<TOut>> (selector async)
        public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
            this Task<Result<TIn>> resultTask,
            Func<TIn, Task<TOut>> selectorAsync)
        {
            var result = await resultTask.ConfigureAwait(false);
            return await result.MapAsync(selectorAsync).ConfigureAwait(false);
        }

        // Task<Result<TIn>> -> Task<Result<TOut>> (selector sync)
        public static async Task<Result<TOut>> Map<TIn, TOut>(
            this Task<Result<TIn>> resultTask,
            Func<TIn, TOut> selector)
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.Map(selector);
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
        // TAP — side-effect khi Success/Failure, không đổi kết quả
        // ======================================================================

        // Result (non-generic) — sync
        public static Result Tap(
            this Result result,
            Action onSuccess)
        {
            if (result.IsSuccess) onSuccess();
            return result;
        }

        // Result (non-generic) — async
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

        // Wrapper cho Task<Result> — sync
        public static async Task<Result> Tap(
            this Task<Result> resultTask,
            Action onSuccess)
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.Tap(onSuccess);
        }

        // Wrapper cho Task<Result>
        public static async Task<Result> TapAsync(
            this Task<Result> resultTask,
            Func<Task> onSuccessAsync)
        {
            var result = await resultTask.ConfigureAwait(false);
            return await result.TapAsync(onSuccessAsync).ConfigureAwait(false);
        }

        // Result<T> — sync
        public static Result<T> Tap<T>(
            this Result<T> result,
            Action<T> onSuccess)
        {
            if (result.IsSuccess)
            {
                onSuccess(result.Value!);
            }
            return result;
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

        // Wrapper cho Task<Result<T>> — sync
        public static async Task<Result<T>> Tap<T>(
            this Task<Result<T>> resultTask,
            Action<T> onSuccess)
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.Tap(onSuccess);
        }

        // Wrapper cho Task<Result<T>>
        public static async Task<Result<T>> TapAsync<T>(
            this Task<Result<T>> resultTask,
            Func<T, Task> onSuccessAsync)
        {
            var result = await resultTask.ConfigureAwait(false);
            return await result.TapAsync(onSuccessAsync).ConfigureAwait(false);
        }

        // ----------------- TAP ERROR -----------------
        public static Result TapError(
            this Result result,
            Action<Error> onFailure)
        {
            if (result.IsFailure) onFailure(result.Error);
            return result;
        }

        public static async Task<Result> TapErrorAsync(
            this Result result,
            Func<Error, Task> onFailureAsync)
        {
            if (result.IsFailure)
            {
                await onFailureAsync(result.Error).ConfigureAwait(false);
            }
            return result;
        }

        public static Result<T> TapError<T>(
            this Result<T> result,
            Action<Error> onFailure)
        {
            if (result.IsFailure) onFailure(result.Error);
            return result;
        }

        public static async Task<Result<T>> TapErrorAsync<T>(
            this Result<T> result,
            Func<Error, Task> onFailureAsync)
        {
            if (result.IsFailure)
            {
                await onFailureAsync(result.Error).ConfigureAwait(false);
            }
            return result;
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

        // Non-generic
        public static Result Try(
            Action action,
            Func<Exception, Error>? errorFactory = null)
        {
            try
            {
                action();
                return Result.Success();
            }
            catch (Exception ex)
            {
                var error = errorFactory is not null ? errorFactory(ex) : CreateUnexpectedError(ex);
                return Result.Failure(error);
            }
        }

        public static async Task<Result> TryAsync(
            Func<Task> actionAsync,
            Func<Exception, Error>? errorFactory = null)
        {
            try
            {
                await actionAsync().ConfigureAwait(false);
                return Result.Success();
            }
            catch (Exception ex)
            {
                var error = errorFactory is not null ? errorFactory(ex) : CreateUnexpectedError(ex);
                return Result.Failure(error);
            }
        }

        // ======================================================================
        // MATCH — giống pattern matching
        // ======================================================================
        public static TOut Match<T, TOut>(
            this Result<T> result,
            Func<T, TOut> onSuccess,
            Func<Error, TOut> onFailure)
        {
            return result.IsSuccess ? onSuccess(result.Value!) : onFailure(result.Error);
        }

        public static async Task<TOut> MatchAsync<T, TOut>(
            this Task<Result<T>> resultTask,
            Func<T, TOut> onSuccess,
            Func<Error, TOut> onFailure)
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.Match(onSuccess, onFailure);
        }

        // ======================================================================
        // MISC
        // ======================================================================

        // Đổi Error khi Failure
        public static Result MapError(this Result result, Func<Error, Error> map)
        {
            return result.IsSuccess ? result : Result.Failure(map(result.Error));
        }

        public static Result<T> MapError<T>(this Result<T> result, Func<Error, Error> map)
        {
            return result.IsSuccess ? result : Result<T>.Failure(map(result.Error));
        }

        public static Result ToResult<T>(this Result<T> result)
        {
            if (result.IsSuccess)
            {
                return Result.Success();
            }
            return Result.Failure(result.Error);
        }

        public static async Task<Result> ToResult<T>(this Task<Result<T>> resultTask)
        {
            var result = await resultTask.ConfigureAwait(false);
            return result.ToResult();
        }

        public static T? GetValueOrDefault<T>(this Result<T> result, T? @default = default)
        {
            return result.IsSuccess ? result.Value : @default;
        }

        private static Error CreateUnexpectedError(Exception ex)
        {
            return new Error(Error.Codes.Unexpected, ex.Message);
        }
    }
}
