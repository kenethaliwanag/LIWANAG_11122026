using System.Net.Http.Json;
using CsvProcessing.Api.Contracts;
using CsvProcessing.Api.Data;
using CsvProcessing.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CsvProcessing.Tests.Integration;

public class UtcTimestampTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;

    public UtcTimestampTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", factory.ApiKey);
    }

    [Fact]
    public async Task UploadedAt_IsStoredAsIntegerUtcTicks()
    {
        var csv = "name,amount\r\nalpha,10\r\n";

        await _client.PostAsync("/api/uploads", UploadRequest.Create("ticks.csv", csv));

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_factory.DatabasePath}")
            .Options;

        await using var db = new AppDbContext(options);

        var columnType = await db.Database
            .SqlQueryRaw<string>("SELECT typeof(UploadedAt) AS \"Value\" FROM ProcessingRuns LIMIT 1")
            .SingleAsync();

        Assert.Equal("integer", columnType);

        var run = await db.ProcessingRuns.AsNoTracking().SingleAsync(item => item.FileName == "ticks.csv");

        Assert.Equal(DateTimeKind.Utc, run.UploadedAt.Kind);
        Assert.Equal(run.UploadedAt.Ticks, run.UploadedAt.ToUniversalTime().Ticks);
    }

    [Fact]
    public async Task History_IsOrderedChronologically_AndCanBeFilteredByDate()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_factory.DatabasePath}")
            .Options;

        var oldest = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var middle = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var newest = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

        await using (var db = new AppDbContext(options))
        {
            db.ProcessingRuns.AddRange(
                new ProcessingRun { FileName = "ordering-newest.csv", UploadedAt = newest, Status = ProcessingStatus.Completed },
                new ProcessingRun { FileName = "ordering-oldest.csv", UploadedAt = oldest, Status = ProcessingStatus.Completed },
                new ProcessingRun { FileName = "ordering-middle.csv", UploadedAt = middle, Status = ProcessingStatus.Completed });

            await db.SaveChangesAsync();
        }

        var history = await _client.GetFromJsonAsync<PagedResponse<ProcessingRunSummaryResponse>>(
            "/api/history?pageSize=100");

        Assert.NotNull(history);

        var ordered = history!.Items
            .Select(item => item.UploadedAt)
            .ToList();

        Assert.Equal(ordered.OrderByDescending(value => value), ordered);

        var filtered = await _client.GetFromJsonAsync<PagedResponse<ProcessingRunSummaryResponse>>(
            "/api/history?pageSize=100&from=2026-05-01T00:00:00Z&to=2026-07-01T00:00:00Z");

        Assert.NotNull(filtered);
        Assert.Contains(filtered!.Items, item => item.FileName == "ordering-middle.csv");
        Assert.DoesNotContain(filtered.Items, item => item.FileName == "ordering-oldest.csv");
        Assert.DoesNotContain(filtered.Items, item => item.FileName == "ordering-newest.csv");
    }
}
