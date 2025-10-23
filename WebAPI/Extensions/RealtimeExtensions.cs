using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WebApi.Options;
using WebAPI.Hubs;

namespace WebApi.Extensions;

public static class RealtimeExtensions
{
    public static IServiceCollection AddRealtime(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSignalR();

        services.AddOptions<RealtimeOptions>()
            .Bind(configuration.GetSection(RealtimeOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ChatPath),
                $"{nameof(RealtimeOptions.ChatPath)} must be provided.");

        return services;
    }

    public static WebApplication MapRealtimeEndpoints(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<RealtimeOptions>>().Value;
        var chatPath = options.ChatPath;

        const string corsPolicyName = "Frontend";

        app.MapHub<PresenceHub>("/ws/presence").RequireCors(corsPolicyName);
        app.MapHub<ChatHub>(chatPath).RequireCors(corsPolicyName);

        if (app.Environment.IsDevelopment())
        {
            app.Logger.LogInformation("Realtime chat hub path resolved to {ChatPath}.", chatPath);

            var endpointSources = app.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();
            var negotiatePath = $"{chatPath}/negotiate".TrimEnd('/');
            var duplicateCount = endpointSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Count(endpoint =>
                {
                    var rawText = endpoint.RoutePattern.RawText;

                    return !string.IsNullOrWhiteSpace(rawText)
                        && string.Equals(rawText.TrimEnd('/'), negotiatePath, StringComparison.OrdinalIgnoreCase);
                });

            if (duplicateCount > 1)
            {
                throw new InvalidOperationException(
                    $"Multiple negotiate endpoints detected for chat hub path '{chatPath}'. Ensure the chat hub is mapped only once.");
            }
        }

        return app;
    }
}
