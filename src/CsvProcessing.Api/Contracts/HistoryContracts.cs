namespace CsvProcessing.Api.Contracts;

public sealed record ProcessingRunSummaryResponse(
    int Id,
    string FileName,
    long FileSizeBytes,
    string Status,
    int RowCount,
    int ColumnCount,
    int NumericColumnCount,
    DateTime UploadedAt,
    DateTime? CompletedAt,
    string? ErrorMessage);

public sealed record ProcessingRunDetailResponse(
    int Id,
    string FileName,
    long FileSizeBytes,
    string Status,
    int RowCount,
    int ColumnCount,
    int NumericColumnCount,
    DateTime UploadedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    IReadOnlyList<ColumnAggregateResponse> Aggregates);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
