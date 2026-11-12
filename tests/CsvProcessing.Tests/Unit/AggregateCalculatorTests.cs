using CsvProcessing.Api.Services;
using Microsoft.AspNetCore.Http;

namespace CsvProcessing.Tests.Unit;

public class AggregateCalculatorTests
{
    private readonly AggregateCalculator _calculator = new();

    [Fact]
    public void Calculate_NumericColumn_ProducesAllAggregates()
    {
        var headers = new[] { "amount" };
        var rows = new[] { new[] { "10" }, new[] { "20" }, new[] { "30" } };

        var aggregates = _calculator.Calculate(headers, rows);

        var aggregate = Assert.Single(aggregates);
        Assert.Equal("amount", aggregate.ColumnName);
        Assert.Equal(3, aggregate.ValueCount);
        Assert.Equal(60m, aggregate.Sum);
        Assert.Equal(20m, aggregate.Average);
        Assert.Equal(10m, aggregate.Minimum);
        Assert.Equal(30m, aggregate.Maximum);
    }

    [Fact]
    public void Calculate_NonNumericColumn_IsExcluded()
    {
        var headers = new[] { "name" };
        var rows = new[] { new[] { "alpha" }, new[] { "beta" } };

        var aggregates = _calculator.Calculate(headers, rows);

        Assert.Empty(aggregates);
    }

    [Fact]
    public void Calculate_ColumnWithOneNonNumericValue_IsExcludedEntirely()
    {
        var headers = new[] { "amount" };
        var rows = new[] { new[] { "10" }, new[] { "twenty" }, new[] { "30" } };

        var aggregates = _calculator.Calculate(headers, rows);

        Assert.Empty(aggregates);
    }

    [Fact]
    public void Calculate_EmptyCells_AreIgnored()
    {
        var headers = new[] { "amount" };
        var rows = new[] { new[] { "10" }, new[] { "" }, new[] { "   " }, new[] { "20" } };

        var aggregate = Assert.Single(_calculator.Calculate(headers, rows));

        Assert.Equal(2, aggregate.ValueCount);
        Assert.Equal(30m, aggregate.Sum);
        Assert.Equal(15m, aggregate.Average);
        Assert.Equal(10m, aggregate.Minimum);
        Assert.Equal(20m, aggregate.Maximum);
    }

    [Fact]
    public void Calculate_ColumnWithOnlyEmptyCells_IsExcluded()
    {
        var headers = new[] { "amount" };
        var rows = new[] { new[] { "" }, new[] { "  " } };

        Assert.Empty(_calculator.Calculate(headers, rows));
    }

    [Fact]
    public void Calculate_MixedColumns_OnlyNumericOnesAreReported()
    {
        var headers = new[] { "name", "amount", "ratio" };
        var rows = new[]
        {
            new[] { "alpha", "10", "0.5" },
            new[] { "beta", "20", "1.5" }
        };

        var aggregates = _calculator.Calculate(headers, rows);

        Assert.Equal(2, aggregates.Count);
        Assert.Equal("amount", aggregates[0].ColumnName);
        Assert.Equal("ratio", aggregates[1].ColumnName);
        Assert.Equal(1.0m, aggregates[1].Average);
    }

    [Fact]
    public void Calculate_UsesInvariantCulture_AndRejectsThousandsSeparators()
    {
        var headers = new[] { "amount" };
        var rows = new[] { new[] { "1234.56" } };

        var aggregate = Assert.Single(_calculator.Calculate(headers, rows));

        Assert.Equal(1234.56m, aggregate.Sum);
    }

    [Fact]
    public void Calculate_ThousandsSeparator_IsNotANumber()
    {
        var headers = new[] { "amount" };
        var rows = new[] { new[] { "1,234" } };

        Assert.Empty(_calculator.Calculate(headers, rows));
    }

    [Fact]
    public void Calculate_NegativeAndDecimalValues_AreHandled()
    {
        var headers = new[] { "amount" };
        var rows = new[] { new[] { "-5.25" }, new[] { "2.75" } };

        var aggregate = Assert.Single(_calculator.Calculate(headers, rows));

        Assert.Equal(-2.5m, aggregate.Sum);
        Assert.Equal(-5.25m, aggregate.Minimum);
        Assert.Equal(2.75m, aggregate.Maximum);
    }

    [Fact]
    public void Calculate_SumOverflow_ThrowsSanitizedError()
    {
        var headers = new[] { "amount" };
        var rows = new[]
        {
            new[] { "79228162514264337593543950335" },
            new[] { "79228162514264337593543950335" }
        };

        var exception = Assert.Throws<CsvProcessingException>(() => _calculator.Calculate(headers, rows));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, exception.StatusCode);
        Assert.Contains("numeric range", exception.Message);
    }
}
