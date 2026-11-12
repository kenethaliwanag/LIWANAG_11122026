using CsvProcessing.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CsvProcessing.Api.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<HealthController> _logger;

    public HealthController(AppDbContext db, ILogger<HealthController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            if (!await _db.Database.CanConnectAsync(cancellationToken))
            {
                _logger.LogWarning("Health check could not reach the database.");

                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "Unhealthy" });
            }

            return Ok(new { status = "Healthy" });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Health check failed with {ExceptionType}.", exception.GetType().Name);

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "Unhealthy" });
        }
    }
}
