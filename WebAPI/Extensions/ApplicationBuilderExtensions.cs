using Scalar.AspNetCore;

namespace WebApi.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseWebApi(this WebApplication app, IHostEnvironment env)
    {
        app.UseExceptionHandler();

        app.MapOpenApi();
        app.MapScalarApiReference("/docs", o => o.WithTitle("Student Gamer Hub API"));
        app.MapGet("/", () => Results.Redirect("/docs"));

        app.UseHttpsRedirection();
        app.UseCors("Default");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }
}
