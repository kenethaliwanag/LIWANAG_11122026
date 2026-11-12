using System.Globalization;
using CsvProcessing.Api.Domain;

namespace CsvProcessing.Api.Services;

public interface IAggregateCalculator
{
    IReadOnlyList<ColumnAggregate> Calculate(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows);
}

public sealed class AggregateCalculator : IAggregateCalculator
{
    private const NumberStyles AllowedNumberStyles =
        NumberStyles.AllowLeadingSign |
        NumberStyles.AllowDecimalPoint |
        NumberStyles.AllowLeadingWhite |
        NumberStyles.AllowTrailingWhite;

    public IReadOnlyList<ColumnAggregate> Calculate(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        var aggregates = new List<ColumnAggregate>();

        for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
        {
            if (!TryCollectValues(rows, columnIndex, out var values))
            {
                continue;
            }

            decimal sum;
            try
            {
                sum = 0m;
                foreach (var value in values)
                {
                    sum = checked(sum + value);
                }
            }
            catch (OverflowException)
            {
                throw new CsvProcessingException(
                    StatusCodes.Status422UnprocessableEntity,
                    $"The total for column '{headers[columnIndex]}' exceeds the supported numeric range.");
            }

            var minimum = values[0];
            var maximum = values[0];
            foreach (var value in values)
            {
                if (value < minimum)
                {
                    minimum = value;
                }

                if (value > maximum)
                {
                    maximum = value;
                }
            }

            aggregates.Add(new ColumnAggregate
            {
                ColumnName = headers[columnIndex],
                ColumnIndex = columnIndex,
                ValueCount = values.Count,
                Sum = sum,
                Average = sum / values.Count,
                Minimum = minimum,
                Maximum = maximum
            });
        }

        return aggregates;
    }

    private static bool TryCollectValues(IReadOnlyList<string[]> rows, int columnIndex, out List<decimal> values)
    {
        values = [];

        foreach (var row in rows)
        {
            if (columnIndex >= row.Length)
            {
                continue;
            }

            var cell = row[columnIndex];
            if (string.IsNullOrWhiteSpace(cell))
            {
                continue;
            }

            if (!decimal.TryParse(cell, AllowedNumberStyles, CultureInfo.InvariantCulture, out var parsed))
            {
                values = [];
                return false;
            }

            values.Add(parsed);
        }

        return values.Count > 0;
    }
}
