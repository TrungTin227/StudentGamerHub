using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

namespace WebApi.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseWebApi(this WebApplication app, IHostEnvironment env)
    {
        app.UseExceptionHandler();

        // Only use HTTPS redirection in development
        // In production (Render.com), the load balancer handles HTTPS
        if (env.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseRouting();
        app.UseCors("Frontend");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        if (env.IsDevelopment())
        {
            var corsOptions = app.Services.GetRequiredService<IOptions<CorsOptions>>().Value;
            var policy = corsOptions.GetPolicy("Frontend");

            if (policy is not null)
            {
                var origins = policy.Origins.Count > 0
                    ? string.Join(", ", policy.Origins)
                    : policy.AllowAnyOrigin ? "*" : "(none)";
                var methods = policy.AllowAnyMethod
                    ? "Any"
                    : policy.Methods.Count > 0 ? string.Join(", ", policy.Methods) : "(none)";
                var headers = policy.AllowAnyHeader
                    ? "Any"
                    : policy.Headers.Count > 0 ? string.Join(", ", policy.Headers) : "(none)";

                app.Logger.LogInformation(
                    "CORS policy 'Frontend' active. Origins: {Origins}. Methods: {Methods}. Headers: {Headers}. SupportsCredentials: {SupportsCredentials}",
                    origins,
                    methods,
                    headers,
                    policy.SupportsCredentials);
            }
            else
            {
                app.Logger.LogWarning("CORS policy 'Frontend' was not found during development diagnostics.");
            }
        }

        app.MapControllers();

        // Map OpenAPI and documentation AFTER all endpoints are mapped
        // so the document includes controller routes (e.g., Clubs endpoints).
        app.MapOpenApi();
        app.MapScalarApiReference("/docs", o => o.WithTitle("Student Gamer Hub API"));
        app.MapGet("/", () => Results.Redirect("/docs"));

        return app;
    }
}
