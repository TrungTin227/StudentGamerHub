using Microsoft.AspNetCore.Mvc;


namespace WebApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer(new BearerSecuritySchemeTransformer());
        });

        services.AddProblemDetails();
        services.AddExceptionHandler<AppExceptionHandler>();

        services.AddCors(opt =>
        {
            opt.AddPolicy("Default", p => p
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod());
        });

        services.Configure<ApiBehaviorOptions>(opt =>
        {
            opt.SuppressModelStateInvalidFilter = false;
        });

        return services;
    }
}
