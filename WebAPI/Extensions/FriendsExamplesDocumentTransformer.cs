using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Globalization;

namespace WebAPI.Extensions
{
    public sealed class FriendsExamplesDocumentTransformer : IOpenApiDocumentTransformer
    {
        // API Path Constants
        private const string FriendsListPath = "/friends";
        private const string InvitePath = "/friends/{userId}/invite";
        private const string AcceptPath = "/friends/{userId}/accept";
        private const string DeclinePath = "/friends/{userId}/decline";
        private const string CancelPath = "/friends/{userId}/cancel";

        // Example Names
        private const string SuccessExampleKey = "Success";
        private const string EmptyListExampleKey = "EmptyList";
        private const string ErrorExampleKey = "Error";

        // Cached status code strings
        private static readonly string Status200 = StatusCodes.Status200OK.ToString(CultureInfo.InvariantCulture);
        private static readonly string Status400 = StatusCodes.Status400BadRequest.ToString(CultureInfo.InvariantCulture);
        private static readonly string Status401 = StatusCodes.Status401Unauthorized.ToString(CultureInfo.InvariantCulture);
        private static readonly string Status403 = StatusCodes.Status403Forbidden.ToString(CultureInfo.InvariantCulture);
        private static readonly string Status404 = StatusCodes.Status404NotFound.ToString(CultureInfo.InvariantCulture);
        private static readonly string Status409 = StatusCodes.Status409Conflict.ToString(CultureInfo.InvariantCulture);
        private static readonly string Status429 = StatusCodes.Status429TooManyRequests.ToString(CultureInfo.InvariantCulture);
        private static readonly string Status500 = StatusCodes.Status500InternalServerError.ToString(CultureInfo.InvariantCulture);

        private static readonly string[] ErrorStatuses =
        [
            Status400,
            Status401,
            Status403,
            Status404,
            Status409,
            Status429,
            Status500
        ];

        private static readonly string[] MutationPaths =
        [
            InvitePath,
            AcceptPath,
            DeclinePath,
            CancelPath
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
            if (!doc.Paths.TryGetValue(FriendsListPath, out var pathItem))
            {
                return;
            }

            if (!pathItem.Operations.TryGetValue(OperationType.Get, out var operation))
            {
                return;
            }

            if (operation.Responses.TryGetValue(Status200, out var okResponse) && okResponse.Content is not null)
            {
                foreach (var mediaType in okResponse.Content.Values)
                {
                    mediaType.Examples ??= new Dictionary<string, OpenApiExample>(StringComparer.Ordinal);
                    mediaType.Examples[SuccessExampleKey] = BuildFriendListExample();
                    mediaType.Examples[EmptyListExampleKey] = BuildEmptyFriendListExample();
                }
            }

            ApplyProblemExamples(operation, FriendsListPath);
        }

        private static void AddMutationExamples(OpenApiDocument doc)
        {
            foreach (var path in MutationPaths)
            {
                if (!doc.Paths.TryGetValue(path, out var pathItem))
                {
                    continue;
                }

                foreach (var operation in pathItem.Operations.Values)
                {
                    ApplyProblemExamples(operation, path);
                }
            }
        }

        private static void ApplyProblemExamples(OpenApiOperation operation, string instancePath)
        {
            foreach (var status in ErrorStatuses)
            {
                if (!operation.Responses.TryGetValue(status, out var response) || response.Content is null)
                {
                    continue;
                }

                AddExamplesToMediaTypes(response.Content, ErrorExampleKey, BuildProblemExample(status, instancePath));
            }
        }

        private static void AddExamplesToMediaTypes(
            IDictionary<string, OpenApiMediaType> content,
            string exampleKey,
            OpenApiExample example)
        {
            foreach (var mediaType in content.Values)
            {
                mediaType.Examples ??= new Dictionary<string, OpenApiExample>(StringComparer.Ordinal);
                mediaType.Examples[exampleKey] = example;
            }
        }

        private static OpenApiExample BuildFriendListExample() => new()
        {
            Summary = "Successful friends list with data",
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
                    },
                    new OpenApiObject
                    {
                        ["Id"] = new OpenApiString("33333333-3333-3333-3333-333333333333"),
                        ["User"] = new OpenApiObject
                        {
                            ["Id"] = new OpenApiString("44444444-4444-4444-4444-444444444444"),
                            ["UserName"] = new OpenApiString("dragon"),
                            ["AvatarUrl"] = new OpenApiString("https://cdn.studentgamerhub.dev/avatars/dragon.png")
                        },
                        ["BecameFriendsAtUtc"] = new OpenApiString("2024-06-20T14:22:15Z")
                    }
                },
                ["NextCursor"] = new OpenApiString("YWJjMTIz"),
                ["PrevCursor"] = new OpenApiNull(),
                ["Size"] = new OpenApiInteger(20),
                ["Sort"] = new OpenApiString("Id"),
                ["Desc"] = new OpenApiBoolean(false)
            }
        };

        private static OpenApiExample BuildEmptyFriendListExample() => new()
        {
            Summary = "Empty friends list",
            Value = new OpenApiObject
            {
                ["Items"] = new OpenApiArray(),
                ["NextCursor"] = new OpenApiNull(),
                ["PrevCursor"] = new OpenApiNull(),
                ["Size"] = new OpenApiInteger(20),
                ["Sort"] = new OpenApiString("Id"),
                ["Desc"] = new OpenApiBoolean(false)
            }
        };

        private static OpenApiExample BuildProblemExample(string statusCode, string instancePath)
        {
            var status = int.Parse(statusCode, CultureInfo.InvariantCulture);
            var (title, type, detail) = status switch
            {
                StatusCodes.Status400BadRequest => (
                    "validation_error",
                    "https://httpstatuses.com/400",
                    "One or more validation errors occurred. Please check the request parameters."
                ),
                StatusCodes.Status401Unauthorized => (
                    "unauthorized",
                    "https://httpstatuses.com/401",
                    "Authentication is required to access this resource. Please provide valid credentials."
                ),
                StatusCodes.Status403Forbidden => (
                    "forbidden",
                    "https://httpstatuses.com/403",
                    "You don't have permission to access this resource."
                ),
                StatusCodes.Status404NotFound => (
                    "not_found",
                    "https://httpstatuses.com/404",
                    "The requested user or friendship was not found."
                ),
                StatusCodes.Status409Conflict => (
                    "conflict",
                    "https://httpstatuses.com/409",
                    "A friendship request already exists or users are already friends."
                ),
                StatusCodes.Status429TooManyRequests => (
                    "too_many_requests",
                    "https://httpstatuses.com/429",
                    "Too many requests. Please try again later."
                ),
                _ => (
                    "unexpected_error",
                    "https://httpstatuses.com/500",
                    "An unexpected error occurred while processing your request."
                )
            };

            return new OpenApiExample
            {
                Summary = $"{status} error",
                Value = new OpenApiObject
                {
                    ["type"] = new OpenApiString(type),
                    ["title"] = new OpenApiString(title),
                    ["status"] = new OpenApiInteger(status),
                    ["detail"] = new OpenApiString(detail),
                    ["instance"] = new OpenApiString(instancePath),
                    ["code"] = new OpenApiString(title),
                    ["traceId"] = new OpenApiString("00-00000000000000000000000000000000-0000000000000000-00")
                }
            };
        }
    }
}
