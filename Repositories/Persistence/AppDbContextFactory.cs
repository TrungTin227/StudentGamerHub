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

            // Load appsettings từ WebAPI project
            TryAddAppSettingsFromSibling(cfgBuilder, currentDir, "WebAPI", env);

            // UserSecrets: Tìm WebAPI project để lấy UserSecretsId
            var webApiProjectPath = FindWebApiProjectPath(currentDir);
            if (webApiProjectPath != null)
            {
                var userSecretsId = ExtractUserSecretsId(webApiProjectPath);
                if (!string.IsNullOrEmpty(userSecretsId))
                {
                    // Use extension method with userSecretsId directly
                    cfgBuilder.AddUserSecrets(userSecretsId);
                }
            }

            // Env vars
            cfgBuilder.AddEnvironmentVariables();

            var config = cfgBuilder.Build();

            // Lấy connection string
            var connectionString =
                  config.GetConnectionString("Default")
               ?? config.GetConnectionString("AppDb")
               ?? throw new InvalidOperationException(
                   "Không tìm thấy connection string ('Default' / 'AppDb'). " +
                   "Vui lòng kiểm tra User Secrets hoặc appsettings.json");

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            optionsBuilder
               .UseNpgsql(connectionString, npgsql =>
               {
                   npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                   npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
               });

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
            if (!Directory.Exists(siblingPath)) return;

            var appsettingsPath = Path.Combine(siblingPath, "appsettings.json");
            var appsettingsDevPath = Path.Combine(siblingPath, $"appsettings.{env}.json");

            if (File.Exists(appsettingsPath))
            {
                var provider = new PhysicalFileProvider(siblingPath);
                builder.AddJsonFile(provider, "appsettings.json", optional: true, reloadOnChange: false);
                
                if (File.Exists(appsettingsDevPath))
                {
                    builder.AddJsonFile(provider, $"appsettings.{env}.json", optional: true, reloadOnChange: false);
                }
            }
        }

        private static string? FindWebApiProjectPath(string currentDir)
        {
            // Tìm file WebAPI.csproj trong thư mục WebAPI
            var webApiPath = Path.Combine(currentDir, "..", "WebAPI");
            var csprojPath = Path.Combine(webApiPath, "WebAPI.csproj");
            
            if (File.Exists(csprojPath))
            {
                return csprojPath;
            }

            return null;
        }

        private static string? ExtractUserSecretsId(string csprojPath)
        {
            try
            {
                var content = File.ReadAllText(csprojPath);
                var startTag = "<UserSecretsId>";
                var endTag = "</UserSecretsId>";
                
                var startIndex = content.IndexOf(startTag);
                if (startIndex < 0) return null;
                
                startIndex += startTag.Length;
                var endIndex = content.IndexOf(endTag, startIndex);
                if (endIndex < 0) return null;
                
                return content.Substring(startIndex, endIndex - startIndex).Trim();
            }
            catch
            {
                return null;
            }
        }
    }
}
