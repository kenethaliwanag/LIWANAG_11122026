using CsvProcessing.Api.Data;

namespace CsvProcessing.Tests.Data;

public class UtcTicksConverterTests
{
    [Fact]
    public void ConvertToProvider_UtcValue_KeepsTicks()
    {
        var value = new DateTime(2026, 11, 11, 8, 30, 0, DateTimeKind.Utc);

        var ticks = Assert.IsType<long>(UtcTicksConverter.Instance.ConvertToProvider(value));

        Assert.Equal(value.Ticks, ticks);
    }

    [Fact]
    public void ConvertToProvider_UnspecifiedValue_IsTreatedAsUtcWithoutShift()
    {
        var value = DateTime.SpecifyKind(new DateTime(2026, 11, 11, 8, 30, 0), DateTimeKind.Unspecified);

        var ticks = Assert.IsType<long>(UtcTicksConverter.Instance.ConvertToProvider(value));

        Assert.Equal(value.Ticks, ticks);
    }

    [Fact]
    public void ConvertToProvider_LocalValue_IsConvertedToUtc()
    {
        var utc = new DateTime(2026, 11, 11, 8, 30, 0, DateTimeKind.Utc);
        var local = utc.ToLocalTime();

        var ticks = Assert.IsType<long>(UtcTicksConverter.Instance.ConvertToProvider(local));

        Assert.Equal(utc.Ticks, ticks);
    }

    [Fact]
    public void ConvertFromProvider_ReturnsUtcKind()
    {
        var utc = new DateTime(2026, 11, 11, 8, 30, 0, DateTimeKind.Utc);

        var value = Assert.IsType<DateTime>(UtcTicksConverter.Instance.ConvertFromProvider(utc.Ticks));

        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(utc, value);
    }
}
