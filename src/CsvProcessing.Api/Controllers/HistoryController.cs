using CsvProcessing.Api.Contracts;
using CsvProcessing.Api.Domain;
using CsvProcessing.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CsvProcessing.Api.Controllers;

[ApiController]
[Route("api/history")]
[Authorize]
[Produces("application/json")]
public class HistoryController : ControllerBase
{
    private const int MaxPageSize = 100;

    private readonly IReportService _reportService;

    public HistoryController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ProcessingRunSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ProcessingRunSummaryResponse>>> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ProcessingStatus? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var result = await _reportService.GetHistoryAsync(
            safePage,
            safePageSize,
            status,
            from,
            to,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{runId:int}")]
    [ProducesResponseType(typeof(ProcessingRunDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProcessingRunDetailResponse>> GetRun(int runId, CancellationToken cancellationToken)
    {
        var run = await _reportService.GetRunAsync(runId, cancellationToken);

        return run is null ? NotFound() : Ok(run);
    }
}
