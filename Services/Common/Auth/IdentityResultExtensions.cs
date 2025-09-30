using Microsoft.AspNetCore.Identity;

namespace Services.Common.Auth;

internal static class IdentityResultExtensions
{
    public static Result ToResult(this IdentityResult r, string? fallback = null)
        => r.Succeeded
            ? Result.Success()
            : Result.Failure(new Error(
                Error.Codes.Validation,
                r.Errors is { } errs && errs.Any()
                    ? string.Join("; ", errs.Select(e => $"{e.Code}: {e.Description}"))
                    : (fallback ?? "Identity operation failed")));

    public static Result<T> ToResult<T>(this IdentityResult r, T payload, string? fallback = null)
        => r.Succeeded
            ? Result<T>.Success(payload)
            : Result<T>.Failure(new Error(
                Error.Codes.Validation,
                r.Errors is { } errs && errs.Any()
                    ? string.Join("; ", errs.Select(e => $"{e.Code}: {e.Description}"))
                    : (fallback ?? "Identity operation failed")));

    public static Error NotFound(string what) => new(Error.Codes.NotFound, $"{what} not found.");
    public static Error ToError(this IdentityResult result, string defaultMessage = "Operation failed.")
    {
        if (result.Succeeded)
            return new Error(Error.Codes.Unexpected, defaultMessage);

        var msg = (result.Errors == null || !result.Errors.Any())
            ? defaultMessage
            : string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));

        // Mapping mặc định coi lỗi Identity là Validation; tuỳ bạn đổi sang Conflict/Unauthorized theo ngữ cảnh
        return new Error(Error.Codes.Validation, msg);
    }
}
