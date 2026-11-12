namespace CsvProcessing.Api.Contracts;

public sealed record ColumnAggregateResponse(
    string ColumnName,
    int ColumnIndex,
    int Count,
    decimal? Sum,
    decimal? Average,
    decimal? Minimum,
    decimal? Maximum);

public sealed record CsvUploadResponse(
    int RunId,
    string FileName,
    string Status,
    int RowCount,
    int ColumnCount,
    int NumericColumnCount,
    DateTime UploadedAt,
    DateTime? CompletedAt,
    IReadOnlyList<ColumnAggregateResponse> Aggregates);

public sealed record CsvUploadErrorResponse(int RunId, string Status, string Error);
