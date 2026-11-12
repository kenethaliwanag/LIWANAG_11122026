using System.Globalization;
using CsvHelper;

namespace CsvProcessing.Api.Services;

public interface ICsvParserService
{
    CsvParseResult Parse(TextReader reader, int maxRows);
}

public sealed class CsvParseResult
{
    public CsvParseResult(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        Headers = headers;
        Rows = rows;
    }

    public IReadOnlyList<string> Headers { get; }

    public IReadOnlyList<string[]> Rows { get; }

    public int RowCount => Rows.Count;
}

public sealed class CsvParserService : ICsvParserService
{
    public CsvParseResult Parse(TextReader reader, int maxRows)
    {
        if (reader.Peek() == -1)
        {
            throw new CsvProcessingException(StatusCodes.Status422UnprocessableEntity, "The CSV file is empty.");
        }

        var options = new CsvReaderOptions
        {
            HasHeader = true,
            Mode = CsvMode.Rfc4180,
            MalformedData = CsvMalformedDataHandling.Throw,
            FieldCountMismatch = CsvFieldCountHandling.Allow,
            MissingField = CsvMissingFieldHandling.ReturnEmpty,
            SkipEmptyLines = true,
            TrimWhitespace = true,
            IncludeValuesInExceptions = false,
            TrackLineNumbers = true,
            FormatProvider = CultureInfo.InvariantCulture
        };

        using var csv = CsvReader.Create(reader, options, leaveOpen: true);

        IReadOnlyList<string> headers;
        try
        {
            csv.ReadHeader();
            headers = (csv.Headers ?? []).Select(header => header.Trim()).ToArray();
        }
        catch (CsvFormatException exception)
        {
            throw Sanitize(exception);
        }
        catch (CsvValidationException)
        {
            throw new CsvProcessingException(
                StatusCodes.Status422UnprocessableEntity,
                "The CSV file could not be read.");
        }

        ValidateHeaders(headers);

        var rows = new List<string[]>();

        try
        {
            while (csv.Read())
            {
                if (rows.Count >= maxRows)
                {
                    throw new CsvProcessingException(
                        StatusCodes.Status422UnprocessableEntity,
                        $"The CSV file exceeds the maximum of {maxRows} data rows.");
                }

                if (csv.FieldCount != headers.Count)
                {
                    throw new CsvProcessingException(
                        StatusCodes.Status422UnprocessableEntity,
                        $"Row {csv.RowNumber} has {csv.FieldCount} fields but the header defines {headers.Count}.");
                }

                var fields = new string[csv.FieldCount];
                for (var index = 0; index < csv.FieldCount; index++)
                {
                    fields[index] = csv.GetString(index);
                }

                rows.Add(fields);
            }
        }
        catch (CsvFormatException exception)
        {
            throw Sanitize(exception);
        }

        if (rows.Count == 0)
        {
            throw new CsvProcessingException(
                StatusCodes.Status422UnprocessableEntity,
                "The CSV file contains a header but no data rows.");
        }

        return new CsvParseResult(headers, rows);
    }

    private static void ValidateHeaders(IReadOnlyList<string> headers)
    {
        if (headers.Count == 0)
        {
            throw new CsvProcessingException(
                StatusCodes.Status422UnprocessableEntity,
                "The CSV file does not contain a header row.");
        }

        for (var index = 0; index < headers.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(headers[index]))
            {
                throw new CsvProcessingException(
                    StatusCodes.Status422UnprocessableEntity,
                    $"The CSV file contains an empty column header at position {index + 1}.");
            }
        }

        var distinct = headers.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (distinct != headers.Count)
        {
            throw new CsvProcessingException(
                StatusCodes.Status422UnprocessableEntity,
                "The CSV file contains duplicate column headers.");
        }
    }

    private static CsvProcessingException Sanitize(CsvFormatException exception) =>
        new(
            StatusCodes.Status422UnprocessableEntity,
            $"Malformed CSV data at record {exception.RecordNumber} (line {exception.LineNumber}).",
            exception.RecordNumber,
            exception.LineNumber);
}
