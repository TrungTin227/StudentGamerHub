using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Net;

namespace WebApi.Common;

public static class ResultHttpExtensions
{
    // -------- MVC Controllers (ControllerBase) --------
    public static ActionResult ToActionResult(this ControllerBase ctrl, Result r)
    {
        if (r.IsSuccess) return ctrl.NoContent();
        return ctrl.Problem(r.Error);
    }

    public static ActionResult ToActionResult<T>(this ControllerBase ctrl, Result<T> r, Func<T, object?>? shape = null, int successStatus = StatusCodes.Status200OK)
    {
        if (r.IsSuccess)
        {
            var payload = shape is null ? r.Value : shape(r.Value!);
            return ctrl.StatusCode(successStatus, payload);
        }
        return ctrl.Problem(r.Error);
    }

    // Tạo 201 CreatedAtAction cho POST
    public static ActionResult ToCreatedAtAction<T>(this ControllerBase ctrl, Result<T> r, string actionName, object? routeValues = null, Func<T, object?>? shape = null)
    {
        if (r.IsSuccess)
        {
            var payload = shape is null ? r.Value : shape(r.Value!);
            return ctrl.CreatedAtAction(actionName, routeValues, payload);
        }
        return ctrl.Problem(r.Error);
    }

    // -------- Minimal API (IResult) --------
    public static IResult ToIResult(this Result r, HttpContext http)
    {
        if (r.IsSuccess) return Results.NoContent();
        return ProblemFromError(http, r.Error);
    }

    public static IResult ToIResult<T>(this Result<T> r, HttpContext http, Func<T, object?>? shape = null, int successStatus = StatusCodes.Status200OK)
    {
        if (r.IsSuccess)
        {
            var payload = shape is null ? r.Value : shape(r.Value!);
            return Results.Json(payload, statusCode: successStatus);
        }
        return ProblemFromError(http, r.Error);
    }

    // -------- Helpers --------
    private static ActionResult Problem(this ControllerBase ctrl, Error error)
    {
        var (status, type) = MapError(error);
        return ctrl.Problem(
            statusCode: status,
            title: error.Code,
            detail: error.Message,
            type: type,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code
            });
    }

    private static IResult ProblemFromError(HttpContext http, Error error)
    {
        var factory = http.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var (status, type) = MapError(error);

        var pd = factory.CreateProblemDetails(
            httpContext: http,
            statusCode: status,
            title: error.Code,
            detail: error.Message,
            type: type);

        pd.Extensions["code"] = error.Code;
        pd.Extensions["traceId"] = http.TraceIdentifier;

        return Results.Problem(
            title: pd.Title,
            detail: pd.Detail,
            statusCode: pd.Status,
            instance: pd.Instance,
            type: pd.Type,
            extensions: pd.Extensions);
    }

    private static (int Status, string Type) MapError(Error e) => e.Code switch
    {
        "validation_error" => ((int)HttpStatusCode.BadRequest, "https://httpstatuses.com/400"),
        "not_found" => ((int)HttpStatusCode.NotFound, "https://httpstatuses.com/404"),
        "conflict" => ((int)HttpStatusCode.Conflict, "https://httpstatuses.com/409"),
        "forbidden" => ((int)HttpStatusCode.Forbidden, "https://httpstatuses.com/403"),
        "unauthorized" => ((int)HttpStatusCode.Unauthorized, "https://httpstatuses.com/401"),
        "service_unavailable" => ((int)HttpStatusCode.ServiceUnavailable, "https://httpstatuses.com/503"),
        _ => ((int)HttpStatusCode.InternalServerError, "https://httpstatuses.com/500")
    };
}
