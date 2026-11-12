using CsvProcessing.Api.Data;
using CsvProcessing.Api.Domain;
using CsvProcessing.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace CsvProcessing.Api.Services;

public sealed record CsvUpload(string FileName, long Length, byte[] Content);

public sealed record ProcessingResult(ProcessingRun Run, bool Succeeded, string? ErrorMessage, int ErrorStatusCode);

public interface IProcessingService
{
    Task<ProcessingResult> ProcessAsync(CsvUpload upload, CancellationToken cancellationToken);
}

public sealed class ProcessingService : IProcessingService
{
    private readonly AppDbContext _db;
    private readonly ICsvFileValidator _validator;
    private readonly ICsvParserService _parser;
    private readonly IAggregateCalculator _calculator;
    private readonly ILogger<ProcessingService> _logger;
    private readonly int _maxRows;

    public ProcessingService(
        AppDbContext db,
        ICsvFileValidator validator,
        ICsvParserService parser,
        IAggregateCalculator calculator,
        IOptions<CsvOptions> options,
        ILogger<ProcessingService> logger)
    {
        _db = db;
        _validator = validator;
        _parser = parser;
        _calculator = calculator;
        _logger = logger;
        _maxRows = options.Value.MaxRows;
    }

    public async Task<ProcessingResult> ProcessAsync(CsvUpload upload, CancellationToken cancellationToken)
    {
        var run = new ProcessingRun
        {
            FileName = SanitizeFileName(upload.FileName),
            FileSizeBytes = upload.Length,
            UploadedAt = DateTime.UtcNow,
            Status = ProcessingStatus.Failed
        };

        _db.ProcessingRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var validation = _validator.Validate(upload.FileName, upload.Content);
            if (!validation.IsValid)
            {
                throw new CsvProcessingException(validation.Rejection!.StatusCode, validation.Rejection.Message);
            }

            using var reader = new StringReader(validation.Text!);
            var parsed = _parser.Parse(reader, _maxRows);
            var aggregates = _calculator.Calculate(parsed.Headers, parsed.Rows);

            run.Status = ProcessingStatus.Completed;
            run.RowCount = parsed.RowCount;
            run.ColumnCount = parsed.Headers.Count;
            run.NumericColumnCount = aggregates.Count;
            run.CompletedAt = DateTime.UtcNow;
            run.Aggregates = aggregates.ToList();

            await _db.SaveChangesAsync(cancellationToken);

            return new ProcessingResult(run, Succeeded: true, ErrorMessage: null, StatusCodes.Status201Created);
        }
        catch (CsvProcessingException exception)
        {
            run.Status = ProcessingStatus.Failed;
            run.ErrorMessage = Truncate(exception.Message);
            run.CompletedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Processing run {RunId} failed with status {StatusCode}.",
                run.Id,
                exception.StatusCode);

            return new ProcessingResult(run, Succeeded: false, run.ErrorMessage, exception.StatusCode);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            run.Status = ProcessingStatus.Failed;
            run.ErrorMessage = "The file could not be processed.";
            run.CompletedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                "Processing run {RunId} failed unexpectedly with {ExceptionType}.",
                run.Id,
                exception.GetType().Name);

            return new ProcessingResult(run, Succeeded: false, run.ErrorMessage, StatusCodes.Status500InternalServerError);
        }
    }

    private static string SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return string.Empty;
        }

        var separatorIndex = fileName.LastIndexOfAny(['/', '\\']);
        var name = separatorIndex >= 0 ? fileName[(separatorIndex + 1)..] : fileName;

        name = new string(name.Where(character => !char.IsControl(character)).ToArray());

        return name.Length <= 260 ? name : name[..260];
    }

    private static string Truncate(string message) =>
        message.Length <= 500 ? message : message[..500];
}
