using CsvProcessing.Api.Services;
using Microsoft.AspNetCore.Http;

namespace CsvProcessing.Tests.Unit;

public class CsvParserServiceTests
{
    private readonly CsvParserService _parser = new();

    [Fact]
    public void Parse_ValidCsv_ReturnsHeadersAndRows()
    {
        using var reader = new StringReader("name,amount\r\nalpha,10\r\nbeta,20\r\n");

        var result = _parser.Parse(reader, maxRows: 100);

        Assert.Equal(["name", "amount"], result.Headers);
        Assert.Equal(2, result.RowCount);
        Assert.Equal(["alpha", "10"], result.Rows[0]);
        Assert.Equal(["beta", "20"], result.Rows[1]);
    }

    [Fact]
    public void Parse_QuotedValuesWithSeparators_AreKeptIntact()
    {
        using var reader = new StringReader("name,note\r\n\"Doe, John\",\"said \"\"hello\"\"\"\r\n");

        var result = _parser.Parse(reader, maxRows: 100);

        Assert.Equal("Doe, John", result.Rows[0][0]);
        Assert.Equal("said \"hello\"", result.Rows[0][1]);
    }

    [Fact]
    public void Parse_TrimsSurroundingWhitespace()
    {
        using var reader = new StringReader(" name , amount \r\n alpha , 10 \r\n");

        var result = _parser.Parse(reader, maxRows: 100);

        Assert.Equal(["name", "amount"], result.Headers);
        Assert.Equal(["alpha", "10"], result.Rows[0]);
    }

    [Fact]
    public void Parse_UnterminatedQuote_ThrowsSanitizedError()
    {
        using var reader = new StringReader("name,amount\r\n\"SECRET-CELL-VALUE,10\r\n");

        var exception = Assert.Throws<CsvProcessingException>(() => _parser.Parse(reader, maxRows: 100));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
        Assert.DoesNotContain("SECRET-CELL-VALUE", exception.Message);
        Assert.Contains("record", exception.Message);
    }

    [Fact]
    public void Parse_RowWithTooFewFields_ThrowsWithRowNumber()
    {
        using var reader = new StringReader("name,amount\r\nalpha,10\r\nbeta\r\n");

        var exception = Assert.Throws<CsvProcessingException>(() => _parser.Parse(reader, maxRows: 100));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
        Assert.Contains("1 fields", exception.Message);
        Assert.Contains("2", exception.Message);
    }

    [Fact]
    public void Parse_RowWithTooManyFields_Throws()
    {
        using var reader = new StringReader("name,amount\r\nalpha,10,extra\r\n");

        var exception = Assert.Throws<CsvProcessingException>(() => _parser.Parse(reader, maxRows: 100));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
        Assert.Contains("3 fields", exception.Message);
    }

    [Fact]
    public void Parse_DuplicateHeaders_Throws()
    {
        using var reader = new StringReader("name,name\r\nalpha,beta\r\n");

        var exception = Assert.Throws<CsvProcessingException>(() => _parser.Parse(reader, maxRows: 100));

        Assert.Contains("duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_EmptyHeaderCell_Throws()
    {
        using var reader = new StringReader("name,\r\nalpha,10\r\n");

        var exception = Assert.Throws<CsvProcessingException>(() => _parser.Parse(reader, maxRows: 100));

        Assert.Contains("empty column header", exception.Message);
    }

    [Fact]
    public void Parse_HeaderWithoutRows_Throws()
    {
        using var reader = new StringReader("name,amount\r\n");

        var exception = Assert.Throws<CsvProcessingException>(() => _parser.Parse(reader, maxRows: 100));

        Assert.Contains("no data rows", exception.Message);
    }

    [Fact]
    public void Parse_EmptyContent_Throws()
    {
        using var reader = new StringReader(string.Empty);

        var exception = Assert.Throws<CsvProcessingException>(() => _parser.Parse(reader, maxRows: 100));

        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_MoreRowsThanAllowed_Throws()
    {
        using var reader = new StringReader("name,amount\r\na,1\r\nb,2\r\nc,3\r\n");

        var exception = Assert.Throws<CsvProcessingException>(() => _parser.Parse(reader, maxRows: 2));

        Assert.Contains("maximum of 2 data rows", exception.Message);
    }

    [Fact]
    public void Parse_BlankLines_AreIgnored()
    {
        using var reader = new StringReader("name,amount\r\n\r\nalpha,10\r\n\r\n");

        var result = _parser.Parse(reader, maxRows: 100);

        Assert.Equal(1, result.RowCount);
    }
}
