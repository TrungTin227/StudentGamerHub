using FluentValidation; 
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace WebApi.ErrorHandling;

public sealed class AppExceptionHandler : IExceptionHandler
{
    private readonly ProblemDetailsFactory _problemDetailsFactory;
    private readonly IHostEnvironment _env;
    private readonly ILogger<AppExceptionHandler> _logger;

    public AppExceptionHandler(
        ProblemDetailsFactory problemDetailsFactory,
        IHostEnvironment env,
        ILogger<AppExceptionHandler> logger)
    {
        _problemDetailsFactory = problemDetailsFactory;
        _env = env;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Unhandled exception");

        (int status, ProblemDetails pd) = exception switch
        {
            // Nếu bạn dùng FluentValidation và ném ValidationException từ Application
            ValidationException fv => (
                StatusCodes.Status400BadRequest,
                BuildValidationProblem(httpContext, fv)
            ),

            // BadHttpRequest (khi payload/headers sai format, Kestrel hay ném)
            BadHttpRequestException badReq => (
                StatusCodes.Status400BadRequest,
                _problemDetailsFactory.CreateProblemDetails(httpContext,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request",
                    detail: badReq.Message,
                    type: "https://httpstatuses.com/400")
            ),

            // Unauthorized
            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                _problemDetailsFactory.CreateProblemDetails(httpContext,
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized",
                    type: "https://httpstatuses.com/401")
            ),

            // NotFound (tuỳ bạn có custom NotFoundException riêng thì match vào đây)
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                _problemDetailsFactory.CreateProblemDetails(httpContext,
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    type: "https://httpstatuses.com/404")
            ),

            // Argument lỗi do input
            ArgumentException arg => (
                StatusCodes.Status400BadRequest,
                _problemDetailsFactory.CreateProblemDetails(httpContext,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request",
                    detail: arg.Message,
                    type: "https://httpstatuses.com/400")
            ),

            // Mặc định: 500
            _ => (
                StatusCodes.Status500InternalServerError,
                _problemDetailsFactory.CreateProblemDetails(httpContext,
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: _env.IsDevelopment() ? exception.Message : "An internal server error occurred",
                    type: "https://httpstatuses.com/500")
            )
        };

        // enrich
        pd.Extensions["traceId"] = httpContext.TraceIdentifier;
        if (_env.IsDevelopment())
        {
            pd.Extensions["exceptionType"] = exception.GetType().FullName;
            pd.Extensions["stackTrace"] = exception.StackTrace;
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(pd, ct);
        return true;
    }

    private ProblemDetails BuildValidationProblem(HttpContext ctx, ValidationException ex)
    {
        // Group errors: PropertyName -> string[]
        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.ErrorMessage).Distinct().ToArray());

        return new ValidationProblemDetails(errors)
        {
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest,
            Type = "https://httpstatuses.com/400"
        };
    }
}
