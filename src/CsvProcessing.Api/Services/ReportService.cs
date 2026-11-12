using CsvProcessing.Api.Contracts;
using CsvProcessing.Api.Data;
using CsvProcessing.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CsvProcessing.Api.Services;

public interface IReportService
{
    Task<PagedResponse<ProcessingRunSummaryResponse>> GetHistoryAsync(
        int page,
        int pageSize,
        ProcessingStatus? status,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken);

    Task<ProcessingRunDetailResponse?> GetRunAsync(int runId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ColumnAggregateResponse>> GetAggregatesAsync(int runId, CancellationToken cancellationToken);

    Task<ReportSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken);
}

public sealed class ReportService : IReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResponse<ProcessingRunSummaryResponse>> GetHistoryAsync(
        int page,
        int pageSize,
        ProcessingStatus? status,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        var query = _db.ProcessingRuns.AsNoTracking();

        if (status is not null)
        {
            var selectedStatus = status.Value;
            query = query.Where(run => run.Status == selectedStatus);
        }

        if (from is not null)
        {
            var fromUtc = from.Value;
            query = query.Where(run => run.UploadedAt >= fromUtc);
        }

        if (to is not null)
        {
            var toUtc = to.Value;
            query = query.Where(run => run.UploadedAt <= toUtc);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(run => run.UploadedAt)
            .ThenByDescending(run => run.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(run => new ProcessingRunSummaryResponse(
                run.Id,
                run.FileName,
                run.FileSizeBytes,
                run.Status.ToString(),
                run.RowCount,
                run.ColumnCount,
                run.NumericColumnCount,
                run.UploadedAt,
                run.CompletedAt,
                run.ErrorMessage))
            .ToListAsync(cancellationToken);

        var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResponse<ProcessingRunSummaryResponse>(items, page, pageSize, totalCount, totalPages);
    }

    public async Task<ProcessingRunDetailResponse?> GetRunAsync(int runId, CancellationToken cancellationToken)
    {
        var run = await _db.ProcessingRuns
            .AsNoTracking()
            .Include(item => item.Aggregates)
            .FirstOrDefaultAsync(item => item.Id == runId, cancellationToken);

        if (run is null)
        {
            return null;
        }

        return new ProcessingRunDetailResponse(
            run.Id,
            run.FileName,
            run.FileSizeBytes,
            run.Status.ToString(),
            run.RowCount,
            run.ColumnCount,
            run.NumericColumnCount,
            run.UploadedAt,
            run.CompletedAt,
            run.ErrorMessage,
            run.Aggregates.ToResponses());
    }

    public async Task<IReadOnlyList<ColumnAggregateResponse>> GetAggregatesAsync(int runId, CancellationToken cancellationToken)
    {
        var aggregates = await _db.ColumnAggregates
            .AsNoTracking()
            .Where(aggregate => aggregate.ProcessingRunId == runId)
            .OrderBy(aggregate => aggregate.ColumnIndex)
            .ToListAsync(cancellationToken);

        return aggregates.ToResponses();
    }

    public async Task<ReportSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var runs = await _db.ProcessingRuns
            .AsNoTracking()
            .Select(run => new { run.Status, run.RowCount, run.FileSizeBytes, run.UploadedAt })
            .ToListAsync(cancellationToken);

        if (runs.Count == 0)
        {
            return new ReportSummaryResponse(0, 0, 0, 0, 0, null, null);
        }

        var earliest = runs.Min(run => run.UploadedAt);
        var latest = runs.Max(run => run.UploadedAt);

        return new ReportSummaryResponse(
            runs.Count,
            runs.Count(run => run.Status == ProcessingStatus.Completed),
            runs.Count(run => run.Status == ProcessingStatus.Failed),
            runs.Sum(run => (long)run.RowCount),
            runs.Sum(run => run.FileSizeBytes),
            earliest,
            latest);
    }
}
