using System.Net;
using System.Net.Http.Json;
using CsvProcessing.Api.Contracts;

namespace CsvProcessing.Tests.Integration;

public class HistoryAndReportTests : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;

    public HistoryAndReportTests(TestApiFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", factory.ApiKey);
    }

    [Fact]
    public async Task FailedProcessing_IsPersistedAndVisibleInHistory()
    {
        var csv = "name,amount\r\n\"unterminated,10\r\n";

        var upload = await _client.PostAsync("/api/uploads", UploadRequest.Create("broken.csv", csv));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, upload.StatusCode);

        var history = await _client.GetFromJsonAsync<PagedResponse<ProcessingRunSummaryResponse>>("/api/history");
        Assert.NotNull(history);

        var failed = history!.Items.Single(item => item.FileName == "broken.csv");
        Assert.Equal("Failed", failed.Status);
        Assert.NotNull(failed.ErrorMessage);
        Assert.NotEmpty(failed.ErrorMessage!);
        Assert.DoesNotContain("unterminated", failed.ErrorMessage!);

        var detail = await _client.GetFromJsonAsync<ProcessingRunDetailResponse>($"/api/history/{failed.Id}");
        Assert.NotNull(detail);
        Assert.Equal("Failed", detail!.Status);
        Assert.Empty(detail.Aggregates);
    }

    [Fact]
    public async Task History_ReturnsCompletedRunWithAggregates()
    {
        var csv = "name,amount\r\nalpha,10\r\nbeta,20\r\n";

        var upload = await _client.PostAsync("/api/uploads", UploadRequest.Create("history.csv", csv));
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);

        var created = await upload.Content.ReadFromJsonAsync<CsvUploadResponse>();
        Assert.NotNull(created);

        var detail = await _client.GetFromJsonAsync<ProcessingRunDetailResponse>($"/api/history/{created!.RunId}");

        Assert.NotNull(detail);
        Assert.Equal("history.csv", detail!.FileName);
        Assert.Equal(2, detail.RowCount);
        Assert.Single(detail.Aggregates);
        Assert.Equal(30m, detail.Aggregates[0].Sum);
    }

    [Fact]
    public async Task History_UnknownRun_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/history/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task History_CanBeFilteredByStatus()
    {
        var failed = await _client.PostAsync("/api/uploads", UploadRequest.Create("filter-failed.csv", "a,b\r\n\"x,y\r\n"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, failed.StatusCode);

        var history = await _client.GetFromJsonAsync<PagedResponse<ProcessingRunSummaryResponse>>("/api/history?status=Failed");

        Assert.NotNull(history);
        Assert.All(history!.Items, item => Assert.Equal("Failed", item.Status));
        Assert.Contains(history.Items, item => item.FileName == "filter-failed.csv");
    }

    [Fact]
    public async Task Report_ReturnsAggregatesOfOneRun()
    {
        var csv = "name,amount\r\nalpha,10\r\nbeta,20\r\n";

        var upload = await _client.PostAsync("/api/uploads", UploadRequest.Create("report.csv", csv));
        var created = await upload.Content.ReadFromJsonAsync<CsvUploadResponse>();

        var aggregates = await _client.GetFromJsonAsync<List<ColumnAggregateResponse>>(
            $"/api/reports/aggregates/{created!.RunId}");

        Assert.NotNull(aggregates);
        var aggregate = Assert.Single(aggregates!);
        Assert.Equal("amount", aggregate.ColumnName);
        Assert.Equal(2, aggregate.Count);
        Assert.Equal(30m, aggregate.Sum);
        Assert.Equal(15m, aggregate.Average);
        Assert.Equal(10m, aggregate.Minimum);
        Assert.Equal(20m, aggregate.Maximum);
    }

    [Fact]
    public async Task Report_ReturnsNotFoundForUnknownRun()
    {
        var response = await _client.GetAsync("/api/reports/aggregates/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReportSummary_CountsRunsAndRows()
    {
        var csv = "name,amount\r\nalpha,10\r\nbeta,20\r\n";

        await _client.PostAsync("/api/uploads", UploadRequest.Create("summary.csv", csv));

        var summary = await _client.GetFromJsonAsync<ReportSummaryResponse>("/api/reports/summary");

        Assert.NotNull(summary);
        Assert.True(summary!.TotalRuns >= 1);
        Assert.True(summary.CompletedRuns >= 1);
        Assert.True(summary.FailedRuns >= 1);
        Assert.True(summary.TotalRowsProcessed >= 2);
        Assert.NotNull(summary.EarliestUploadAt);
        Assert.NotNull(summary.LatestUploadAt);
        Assert.True(summary.LatestUploadAt >= summary.EarliestUploadAt);
    }
}
