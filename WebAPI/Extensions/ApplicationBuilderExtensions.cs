using Scalar.AspNetCore;

namespace WebApi.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseWebApi(this WebApplication app, IHostEnvironment env)
    {
        app.UseExceptionHandler();

        if (env.IsDevelopment())
        {
            // 1) Xuất OpenAPI JSON
            app.MapOpenApi();

            // 2) Map Scalar UI (chọn route cố định, ví dụ /docs)
            app.MapScalarApiReference("/docs", o => o.WithTitle("My API"));

            // 3) Redirect trang gốc "/" -> "/docs" để mở UI ngay khi chạy
            app.MapGet("/", () => Results.Redirect("/docs"));
        }

        app.UseHttpsRedirection();
        app.UseCors("Default");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }
}
