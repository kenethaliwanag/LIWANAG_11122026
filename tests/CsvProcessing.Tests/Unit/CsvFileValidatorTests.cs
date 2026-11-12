using System.Text;
using CsvProcessing.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CsvProcessing.Tests.Unit;

public class CsvFileValidatorTests
{
    private readonly CsvFileValidator _validator = new(Options.Create(new CsvOptions
    {
        MaxFileSizeBytes = 5 * 1024 * 1024,
        MaxRows = 100_000
    }));

    [Fact]
    public void Validate_ValidCsv_IsAccepted()
    {
        var content = Encoding.UTF8.GetBytes("name,amount\r\nalpha,10\r\n");

        var result = _validator.Validate("data.csv", content);

        Assert.True(result.IsValid);
        Assert.Equal("name,amount\r\nalpha,10\r\n", result.Text);
    }

    [Fact]
    public void Validate_Utf8ByteOrderMark_IsStripped()
    {
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("name\r\nalpha\r\n")).ToArray();

        var result = _validator.Validate("data.csv", content);

        Assert.True(result.IsValid);
        Assert.StartsWith("name", result.Text);
    }

    [Theory]
    [InlineData("data.txt")]
    [InlineData("data")]
    [InlineData("data.csv.exe")]
    public void Validate_WrongExtension_IsRejected(string fileName)
    {
        var result = _validator.Validate(fileName, Encoding.UTF8.GetBytes("name\r\n"));

        Assert.False(result.IsValid);
        Assert.Equal(StatusCodes.Status400BadRequest, result.Rejection!.StatusCode);
    }

    [Fact]
    public void Validate_UppercaseExtension_IsAccepted()
    {
        var result = _validator.Validate("DATA.CSV", Encoding.UTF8.GetBytes("name\r\nalpha\r\n"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_TooLarge_IsRejected()
    {
        var content = new byte[(5 * 1024 * 1024) + 1];

        var result = _validator.Validate("data.csv", content);

        Assert.False(result.IsValid);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, result.Rejection!.StatusCode);
    }

    [Fact]
    public void Validate_InvalidUtf8_IsRejected()
    {
        var content = new byte[] { 0x6E, 0x61, 0x6D, 0x65, 0xFF, 0xFE, 0x0A };

        var result = _validator.Validate("data.csv", content);

        Assert.False(result.IsValid);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.Rejection!.StatusCode);
        Assert.Contains("UTF-8", result.Rejection.Message);
    }

    [Fact]
    public void Validate_Utf16ByteOrderMark_IsRejected()
    {
        var content = new byte[] { 0xFF, 0xFE, 0x6E, 0x00 };

        var result = _validator.Validate("data.csv", content);

        Assert.False(result.IsValid);
        Assert.Contains("UTF-8", result.Rejection!.Message);
    }

    [Fact]
    public void Validate_BinaryContent_IsRejected()
    {
        var content = new byte[] { 0x6E, 0x61, 0x00, 0x6D, 0x65 };

        var result = _validator.Validate("data.csv", content);

        Assert.False(result.IsValid);
        Assert.Contains("binary", result.Rejection!.Message);
    }

    [Fact]
    public void Validate_EmptyFile_IsRejected()
    {
        var result = _validator.Validate("data.csv", []);

        Assert.False(result.IsValid);
        Assert.Contains("empty", result.Rejection!.Message, StringComparison.OrdinalIgnoreCase);
    }
}
