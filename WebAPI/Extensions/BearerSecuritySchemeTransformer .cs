using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace WebAPI.Extensions
{
    public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(OpenApiDocument doc, OpenApiDocumentTransformerContext ctx, CancellationToken ct)
        {
            doc.Components ??= new OpenApiComponents();
            doc.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();

            doc.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "Nhập token theo dạng: Bearer {access_token}"
            };

            doc.SecurityRequirements ??= new List<OpenApiSecurityRequirement>();
            doc.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

            return Task.CompletedTask;
        }
    }
}
