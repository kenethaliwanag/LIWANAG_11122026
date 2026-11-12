namespace CsvProcessing.Api.Services;

public sealed class CsvProcessingException : Exception
{
    public CsvProcessingException(int statusCode, string message, long? recordNumber = null, long? lineNumber = null)
        : base(message)
    {
        StatusCode = statusCode;
        RecordNumber = recordNumber;
        LineNumber = lineNumber;
    }

    public int StatusCode { get; }

    public long? RecordNumber { get; }

    public long? LineNumber { get; }
}
