namespace CsvProcessing.Api.Contracts;

public sealed record UploadRejection(int StatusCode, string Message);
