namespace Repositories.Persistence.Seeding;

/// <summary>
/// Application data seeder executed during startup.
/// </summary>
public interface IAppSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}
