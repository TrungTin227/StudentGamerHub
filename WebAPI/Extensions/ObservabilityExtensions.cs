namespace WebApi.Extensions;

public static class ObservabilityExtensions
{
    public static void AddObservability(this IHostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        // Có thể thay bằng Serilog nếu muốn.
        builder.Services.AddHttpContextAccessor();
    }
}
