using CsvProcessing.Api.Contracts;
using CsvProcessing.Api.Domain;

namespace CsvProcessing.Api.Services;

public static class ColumnAggregateMapping
{
    public static IReadOnlyList<ColumnAggregateResponse> ToResponses(this IEnumerable<ColumnAggregate> aggregates) =>
        aggregates
            .OrderBy(aggregate => aggregate.ColumnIndex)
            .Select(aggregate => new ColumnAggregateResponse(
                aggregate.ColumnName,
                aggregate.ColumnIndex,
                aggregate.ValueCount,
                aggregate.Sum,
                aggregate.Average,
                aggregate.Minimum,
                aggregate.Maximum))
            .ToList();
}
