using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CsvProcessing.Api.Data;

public static class UtcTicksConverter
{
    public static readonly ValueConverter<DateTime, long> Instance = new(
        value => value.Kind == DateTimeKind.Utc
            ? value.Ticks
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime().Ticks
                : DateTime.SpecifyKind(value, DateTimeKind.Utc).Ticks,
        ticks => new DateTime(ticks, DateTimeKind.Utc));
}
