using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Globalization;

namespace WebAPI.Extensions
{
    public sealed class FriendsExamplesDocumentTransformer : IOpenApiDocumentTransformer
    {
        private static readonly string[] ErrorStatuses =
        [
            StatusCodes.Status400BadRequest.ToString(CultureInfo.InvariantCulture),
            StatusCodes.Status401Unauthorized.ToString(CultureInfo.InvariantCulture),
            StatusCodes.Status403Forbidden.ToString(CultureInfo.InvariantCulture),
            StatusCodes.Status404NotFound.ToString(CultureInfo.InvariantCulture),
            StatusCodes.Status409Conflict.ToString(CultureInfo.InvariantCulture),
            StatusCodes.Status429TooManyRequests.ToString(CultureInfo.InvariantCulture),
            StatusCodes.Status500InternalServerError.ToString(CultureInfo.InvariantCulture)
        ];

        public Task TransformAsync(OpenApiDocument doc, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            if (doc.Paths is null || doc.Paths.Count == 0)
            {
                return Task.CompletedTask;
            }

            AddListExamples(doc);
            AddMutationExamples(doc);

            return Task.CompletedTask;
        }

        private static void AddListExamples(OpenApiDocument doc)
        {
            if (!doc.Paths.TryGetValue("/friends", out var pathItem))
            {
                return;
            }

            if (!pathItem.Operations.TryGetValue(OperationType.Get, out var operation))
            {
                return;
            }

            if (operation.Responses.TryGetValue(StatusCodes.Status200OK.ToString(CultureInfo.InvariantCulture), out var okResponse))
            {
                foreach (var mediaType in okResponse.Content.Values)
                {
                    mediaType.Examples ??= new Dictionary<string, OpenApiExample>(StringComparer.Ordinal);
                    mediaType.Examples["Success"] = BuildFriendListExample();
                }
            }

            ApplyProblemExamples(operation);
        }

        private static void AddMutationExamples(OpenApiDocument doc)
        {
            foreach (var path in new[]
            {
                "/friends/{userId}/invite",
                "/friends/{userId}/accept",
                "/friends/{userId}/decline",
                "/friends/{userId}/cancel"
            })
            {
                if (!doc.Paths.TryGetValue(path, out var pathItem))
                {
                    continue;
                }

                foreach (var operation in pathItem.Operations.Values)
                {
                    ApplyProblemExamples(operation);
                }
            }
        }

        private static void ApplyProblemExamples(OpenApiOperation operation)
        {
            foreach (var status in ErrorStatuses)
            {
                if (!operation.Responses.TryGetValue(status, out var response))
                {
                    continue;
                }

                foreach (var mediaType in response.Content.Values)
                {
                    mediaType.Examples ??= new Dictionary<string, OpenApiExample>(StringComparer.Ordinal);
                    mediaType.Examples["Error"] = BuildProblemExample(status);
                }
            }
        }

        private static OpenApiExample BuildFriendListExample() => new()
        {
            Summary = "Friends list",
            Value = new OpenApiObject
            {
                ["Items"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["Id"] = new OpenApiString("11111111-1111-1111-1111-111111111111"),
                        ["User"] = new OpenApiObject
                        {
                            ["Id"] = new OpenApiString("22222222-2222-2222-2222-222222222222"),
                            ["UserName"] = new OpenApiString("phoenix"),
                            ["AvatarUrl"] = new OpenApiString("https://cdn.studentgamerhub.dev/avatars/phoenix.png")
                        },
                        ["BecameFriendsAtUtc"] = new OpenApiString("2024-05-12T08:15:30Z")
                    }
                },
                ["NextCursor"] = new OpenApiString("YWJjMTIz"),
                ["PrevCursor"] = OpenApiNull.Instance,
                ["Size"] = new OpenApiInteger(20),
                ["Sort"] = new OpenApiString("Id"),
                ["Desc"] = new OpenApiBoolean(false)
            }
        };

        private static OpenApiExample BuildProblemExample(string statusCode)
        {
            var status = int.Parse(statusCode, CultureInfo.InvariantCulture);
            var (title, type) = status switch
            {
                StatusCodes.Status400BadRequest => ("validation_error", "https://httpstatuses.com/400"),
                StatusCodes.Status401Unauthorized => ("unauthorized", "https://httpstatuses.com/401"),
                StatusCodes.Status403Forbidden => ("forbidden", "https://httpstatuses.com/403"),
                StatusCodes.Status404NotFound => ("not_found", "https://httpstatuses.com/404"),
                StatusCodes.Status409Conflict => ("conflict", "https://httpstatuses.com/409"),
                StatusCodes.Status429TooManyRequests => ("too_many_requests", "https://httpstatuses.com/429"),
                _ => ("unexpected_error", "https://httpstatuses.com/500")
            };

            return new OpenApiExample
            {
                Summary = $"{status} error",
                Value = new OpenApiObject
                {
                    ["type"] = new OpenApiString(type),
                    ["title"] = new OpenApiString(title),
                    ["status"] = new OpenApiInteger(status),
                    ["detail"] = new OpenApiString("Example error message."),
                    ["instance"] = new OpenApiString("/friends"),
                    ["code"] = new OpenApiString(title),
                    ["traceId"] = new OpenApiString("00-00000000000000000000000000000000-0000000000000000-00")
                }
            };
        }
    }
}
