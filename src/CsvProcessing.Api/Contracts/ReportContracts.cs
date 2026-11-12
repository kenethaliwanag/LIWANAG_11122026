namespace CsvProcessing.Api.Contracts;

public sealed record ReportSummaryResponse(
    int TotalRuns,
    int CompletedRuns,
    int FailedRuns,
    long TotalRowsProcessed,
    long TotalBytesProcessed,
    DateTime? EarliestUploadAt,
    DateTime? LatestUploadAt);
