using FluentValidation;

namespace Services.Common.Results
{
    public static class ValidationHelpers
    {
        public static async Task<Result<T>> ValidateToResultAsync<T>(
            this IValidator<T> validator,
            T model,
            CancellationToken ct = default)
        {
            var vr = await validator.ValidateAsync(model, ct).ConfigureAwait(false);
            if (vr.IsValid)
            {
                return Result<T>.Success(model);
            }

            string msg = string.Join("; ", vr.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            return Result<T>.Failure(new Error(Error.Codes.Validation, msg));
        }
    }
}
