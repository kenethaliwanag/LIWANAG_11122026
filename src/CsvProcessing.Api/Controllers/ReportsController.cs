using CsvProcessing.Api.Contracts;
using CsvProcessing.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CsvProcessing.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
[Produces("application/json")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("aggregates/{runId:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<ColumnAggregateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ColumnAggregateResponse>>> GetAggregates(
        int runId,
        CancellationToken cancellationToken)
    {
        var run = await _reportService.GetRunAsync(runId, cancellationToken);
        if (run is null)
        {
            return NotFound();
        }

        return Ok(await _reportService.GetAggregatesAsync(runId, cancellationToken));
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(ReportSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportSummaryResponse>> GetSummary(CancellationToken cancellationToken)
    {
        return Ok(await _reportService.GetSummaryAsync(cancellationToken));
    }
}
