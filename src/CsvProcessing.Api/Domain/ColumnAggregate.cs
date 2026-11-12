namespace CsvProcessing.Api.Domain;

public class ColumnAggregate
{
    public int Id { get; set; }

    public int ProcessingRunId { get; set; }

    public string ColumnName { get; set; } = string.Empty;

    public int ColumnIndex { get; set; }

    public int ValueCount { get; set; }

    public decimal? Sum { get; set; }

    public decimal? Average { get; set; }

    public decimal? Minimum { get; set; }

    public decimal? Maximum { get; set; }

    public ProcessingRun ProcessingRun { get; set; } = null!;
}
