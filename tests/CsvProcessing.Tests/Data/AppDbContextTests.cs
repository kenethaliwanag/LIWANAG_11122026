using CsvProcessing.Api.Data;
using CsvProcessing.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CsvProcessing.Tests.Data;

public class AppDbContextTests : IDisposable
{
    private readonly string _databasePath;
    private readonly AppDbContext _db;

    public AppDbContextTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"csvprocessing-tests-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            var file = _databasePath + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    [Fact]
    public async Task UploadedAt_IsStoredAsIntegerUtcTicks_AndRoundTripsAsUtc()
    {
        var uploadedAt = new DateTime(2026, 11, 11, 8, 30, 0, DateTimeKind.Utc);

        _db.ProcessingRuns.Add(new ProcessingRun
        {
            FileName = "sample.csv",
            FileSizeBytes = 128,
            UploadedAt = uploadedAt,
            Status = ProcessingStatus.Completed
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var stored = await _db.ProcessingRuns.SingleAsync();

        Assert.Equal(DateTimeKind.Utc, stored.UploadedAt.Kind);
        Assert.Equal(uploadedAt, stored.UploadedAt);

        var columnType = await _db.Database
            .SqlQueryRaw<string>("SELECT typeof(UploadedAt) AS \"Value\" FROM ProcessingRuns")
            .SingleAsync();

        Assert.Equal("integer", columnType);
    }

    [Fact]
    public async Task CompletedAt_Null_RoundTripsAsNull()
    {
        _db.ProcessingRuns.Add(new ProcessingRun
        {
            FileName = "sample.csv",
            UploadedAt = DateTime.UtcNow,
            CompletedAt = null,
            Status = ProcessingStatus.Failed
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var stored = await _db.ProcessingRuns.SingleAsync();

        Assert.Null(stored.CompletedAt);
    }

    [Fact]
    public async Task CompletedAt_NonNull_RoundTripsAsUtc()
    {
        var completedAt = new DateTime(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc);

        _db.ProcessingRuns.Add(new ProcessingRun
        {
            FileName = "sample.csv",
            UploadedAt = completedAt.AddMinutes(-5),
            CompletedAt = completedAt,
            Status = ProcessingStatus.Completed
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var stored = await _db.ProcessingRuns.SingleAsync();

        Assert.NotNull(stored.CompletedAt);
        Assert.Equal(DateTimeKind.Utc, stored.CompletedAt!.Value.Kind);
        Assert.Equal(completedAt, stored.CompletedAt);
    }

    [Fact]
    public async Task Aggregates_AreCascadeDeletedWithTheirRun()
    {
        var run = new ProcessingRun
        {
            FileName = "sample.csv",
            UploadedAt = DateTime.UtcNow,
            Status = ProcessingStatus.Completed
        };
        run.Aggregates.Add(new ColumnAggregate
        {
            ColumnName = "amount",
            ColumnIndex = 0,
            ValueCount = 2,
            Sum = 30m,
            Average = 15m,
            Minimum = 10m,
            Maximum = 20m
        });

        _db.ProcessingRuns.Add(run);
        await _db.SaveChangesAsync();

        _db.ProcessingRuns.Remove(run);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        Assert.Empty(await _db.ProcessingRuns.ToListAsync());
        Assert.Empty(await _db.ColumnAggregates.ToListAsync());
    }
}
