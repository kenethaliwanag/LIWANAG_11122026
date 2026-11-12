using System.Text;
using CsvProcessing.Api.Contracts;
using Microsoft.Extensions.Options;

namespace CsvProcessing.Api.Infrastructure;

public sealed class CsvOptions
{
    public const string SectionName = "Csv";

    public int MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    public int MaxRows { get; set; } = 100_000;
}

public interface ICsvFileValidator
{
    CsvFileValidationResult Validate(string fileName, byte[] content);
}

public sealed class CsvFileValidationResult
{
    private CsvFileValidationResult(string? text, UploadRejection? rejection)
    {
        Text = text;
        Rejection = rejection;
    }

    public string? Text { get; }

    public UploadRejection? Rejection { get; }

    public bool IsValid => Rejection is null;

    public static CsvFileValidationResult Valid(string text) => new(text, null);

    public static CsvFileValidationResult Invalid(int statusCode, string message) =>
        new(null, new UploadRejection(statusCode, message));
}

public sealed class CsvFileValidator : ICsvFileValidator
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly int _maxFileSizeBytes;

    public CsvFileValidator(IOptions<CsvOptions> options)
    {
        _maxFileSizeBytes = options.Value.MaxFileSizeBytes;
    }

    public CsvFileValidationResult Validate(string fileName, byte[] content)
    {
        if (!HasCsvExtension(fileName))
        {
            return CsvFileValidationResult.Invalid(
                StatusCodes.Status400BadRequest,
                "The uploaded file must have a .csv extension.");
        }

        if (content.Length > _maxFileSizeBytes)
        {
            return CsvFileValidationResult.Invalid(
                StatusCodes.Status413PayloadTooLarge,
                $"The uploaded file exceeds the maximum size of {_maxFileSizeBytes} bytes.");
        }

        if (content.Length == 0)
        {
            return CsvFileValidationResult.Invalid(
                StatusCodes.Status422UnprocessableEntity,
                "The uploaded file is empty.");
        }

        if (HasUnsupportedByteOrderMark(content))
        {
            return CsvFileValidationResult.Invalid(
                StatusCodes.Status422UnprocessableEntity,
                "The uploaded file must be UTF-8 encoded.");
        }

        var body = StripUtf8Bom(content);

        if (Array.IndexOf(body, (byte)0x00) >= 0)
        {
            return CsvFileValidationResult.Invalid(
                StatusCodes.Status422UnprocessableEntity,
                "The uploaded file appears to be binary rather than CSV text.");
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(body);
        }
        catch (DecoderFallbackException)
        {
            return CsvFileValidationResult.Invalid(
                StatusCodes.Status422UnprocessableEntity,
                "The uploaded file is not valid UTF-8 text.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return CsvFileValidationResult.Invalid(
                StatusCodes.Status422UnprocessableEntity,
                "The uploaded file is empty.");
        }

        return CsvFileValidationResult.Valid(text);
    }

    private static bool HasCsvExtension(string fileName) =>
        fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

    private static bool HasUnsupportedByteOrderMark(byte[] content)
    {
        if (content.Length >= 4 && content[0] == 0xFF && content[1] == 0xFE && content[2] == 0x00 && content[3] == 0x00)
        {
            return true;
        }

        if (content.Length >= 4 && content[0] == 0x00 && content[1] == 0x00 && content[2] == 0xFE && content[3] == 0xFF)
        {
            return true;
        }

        if (content.Length >= 2 && content[0] == 0xFF && content[1] == 0xFE)
        {
            return true;
        }

        return content.Length >= 2 && content[0] == 0xFE && content[1] == 0xFF;
    }

    private static byte[] StripUtf8Bom(byte[] content)
    {
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
        {
            return content[3..];
        }

        return content;
    }
}
