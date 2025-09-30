using Microsoft.AspNetCore.HttpOverrides;

namespace WebApi.Extensions;

public static class OperationalAppExtensions
{
    public static WebApplication UseOperationalPipeline(this WebApplication app, IHostEnvironment env)
    {
        // Nếu sau này chạy sau reverse proxy (Nginx/Traefik), bật forwarded headers:
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        if (!env.IsDevelopment())
        {
            app.UseHsts(); // bật HSTS khi prod
        }

        app.UseResponseCompression();
        app.UseResponseCaching();
        app.UseRateLimiter();
        app.UseRequestTimeouts();

        // Health endpoints
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/live");
        app.MapHealthChecks("/ready");

        return app;
    }
}
