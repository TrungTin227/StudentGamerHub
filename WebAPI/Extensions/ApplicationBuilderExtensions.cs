using Scalar.AspNetCore;

namespace WebApi.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseWebApi(this WebApplication app, IHostEnvironment env)
    {
        app.UseExceptionHandler();

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseCors("Default");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        // Map OpenAPI and documentation AFTER all endpoints are mapped
        // so the document includes controller routes (e.g., Clubs endpoints).
        app.MapOpenApi();
        app.MapScalarApiReference("/docs", o => o.WithTitle("Student Gamer Hub API"));
        app.MapGet("/", () => Results.Redirect("/docs"));

        return app;
    }
}
