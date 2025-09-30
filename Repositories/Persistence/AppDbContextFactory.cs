using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using System.Reflection;

namespace Repositories.Persistence
{
    public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                      ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                      ?? "Development";

            var currentDir = Directory.GetCurrentDirectory();

            var cfgBuilder = new ConfigurationBuilder()
                .SetBasePath(currentDir)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false);

            // Nếu đang gọi từ lớp Infrastructure/Repositories, thử load appsettings của WebApi (startup project)
            TryAddAppSettingsFromSibling(cfgBuilder, currentDir, "WebAPI", env);
            //TryAddAppSettingsFromSibling(cfgBuilder, currentDir, "Presentation", env); // tuỳ naming

            // UserSecrets (nếu project WebApi có khai báo)
            TryAddUserSecrets(cfgBuilder, "WebAPI");      // tên assembly startup của bạn
            cfgBuilder.AddUserSecrets<AppDbContextFactory>(optional: true);

            // Env vars
            cfgBuilder.AddEnvironmentVariables();

            var config = cfgBuilder.Build();

            // Lấy connection string (đổi tên key theo appsettings của bạn)
            var connectionString =
                  config.GetConnectionString("Default")
               ?? config.GetConnectionString("AppDb")
               ?? throw new InvalidOperationException(
                   "Không tìm thấy connection string ('Default' / 'AppDb').");

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            optionsBuilder
               .UseNpgsql(connectionString, npgsql =>
               {
                   npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                   // retry transient lỗi mạng/PG server
                   npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
               });

            // Khi chạy design-time trong Dev, bật log chi tiết nếu muốn
            if (string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase))
            {
                optionsBuilder.EnableDetailedErrors();
                optionsBuilder.EnableSensitiveDataLogging();
            }

            return new AppDbContext(optionsBuilder.Options);
        }

        private static void TryAddAppSettingsFromSibling(
            IConfigurationBuilder builder,
            string currentDir,
            string siblingProjectFolderName,
            string env)
        {
            var siblingPath = Path.Combine(currentDir, "..", siblingProjectFolderName);
            var appsettings = Path.Combine(siblingPath, "appsettings.Development.json");
            if (!File.Exists(appsettings)) return;

            var provider = new PhysicalFileProvider(siblingPath);
            builder.AddJsonFile(provider, "appsettings.json", optional: true, reloadOnChange: false);
            builder.AddJsonFile(provider, $"appsettings.{env}.json", optional: true, reloadOnChange: false);
        }

        private static void TryAddUserSecrets(IConfigurationBuilder builder, string startupAssemblyName)
        {
            try
            {
                var asm = Assembly.Load(new AssemblyName(startupAssemblyName));
                builder.AddUserSecrets(asm, optional: true);
            }
            catch
            {
                // Ignored: nếu không load được WebApi, vẫn tiếp tục với nguồn khác
            }
        }
    }
}
