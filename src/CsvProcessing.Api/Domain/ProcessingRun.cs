namespace CsvProcessing.Api.Domain;

public class ProcessingRun
{
    public int Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int RowCount { get; set; }

    public int ColumnCount { get; set; }

    public int NumericColumnCount { get; set; }

    public ProcessingStatus Status { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime UploadedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public ICollection<ColumnAggregate> Aggregates { get; set; } = new List<ColumnAggregate>();
}
