
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();
// Data Protection: bắt buộc khi dùng AddDefaultTokenProviders()
builder.Services.AddDataProtection();

// Prod: lưu key bền vững + app name để scale nhiều instance
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(
            new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")))
        .SetApplicationName("StudentGamerHub"); // đặt tên cố định cho app
}
builder.Services.AddEmailing(builder.Configuration);
builder.Services
    .AddWebApi(builder.Configuration)
    .AddRealtime(builder.Configuration)
    .AddOperationalServices(builder.Configuration)
    .AddDataLayer(builder.Configuration)
    .AddJwtAuth<User>(builder.Configuration)
    .AddApplicationServices(builder.Configuration);

var app = builder.Build();

// Thứ tự gọi 2 pipeline này đã rất tốt.
// UseOperationalPipeline xử lý các vấn đề cross-cutting ở tầng thấp (proxy, HSTS, rate limiting).
// UseWebApi xử lý các vấn đề của khung sườn API (lỗi, OpenAPI, HTTPS, CORS, Auth, RateLimiter, Controllers).
app.UseOperationalPipeline(app.Environment);
app.UseWebApi(app.Environment);

app.MapRealtimeEndpoints();

app.Run();

public partial class Program { }

namespace WebAPI
{
    public partial class Program { }
}
