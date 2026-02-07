using Npgsql;

var cs = "Host=aws-1-ap-south-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.ssbitctpwtczfyvkhrbq;Password=0937213289Tin@;SSL Mode=Require;Trust Server Certificate=true;Pooling=false;Timeout=30";

using var conn = new NpgsqlConnection(cs);
conn.Open();

// 1) List all tables in public schema
Console.WriteLine("=== TABLES IN public SCHEMA ===");
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = @"
        SELECT table_name 
        FROM information_schema.tables 
        WHERE table_schema = 'public' 
        ORDER BY table_name;";
    using var reader = cmd.ExecuteReader();
    int count = 0;
    while (reader.Read())
    {
        Console.WriteLine($"  {++count}. {reader.GetString(0)}");
    }
    if (count == 0) Console.WriteLine("  (no tables found)");
    else Console.WriteLine($"\nTotal: {count} tables");
}

// 2) Check __EFMigrationsHistory
Console.WriteLine("\n=== EF MIGRATIONS HISTORY ===");
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = @"SELECT ""MigrationId"", ""ProductVersion"" FROM ""__EFMigrationsHistory"" ORDER BY ""MigrationId"";";
    try
    {
        using var reader = cmd.ExecuteReader();
        int count = 0;
        while (reader.Read())
        {
            Console.WriteLine($"  {reader.GetString(0)} (EF {reader.GetString(1)})");
            count++;
        }
        if (count == 0) Console.WriteLine("  (no migration records - table exists but empty)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Error: {ex.Message}");
    }
}
