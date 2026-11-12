using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace CsvProcessing.Tests;

public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"csvprocessing-api-{Guid.NewGuid():N}.db");

    public string ApiKey { get; } = $"test-key-{Guid.NewGuid():N}";

    public string DatabasePath => _databasePath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ApiKey:Key", ApiKey);
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_databasePath}");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            var file = _databasePath + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
