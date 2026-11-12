using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CsvProcessing.Api.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DesignTimeConnectionString = "Data Source=App_Data/csvprocessing.db";

    public AppDbContext CreateDbContext(string[] args)
    {
        SqliteDatabaseLocation.EnsureDirectoryExists(DesignTimeConnectionString);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(DesignTimeConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}
