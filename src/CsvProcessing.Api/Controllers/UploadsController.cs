using CsvProcessing.Api.Contracts;
using CsvProcessing.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CsvProcessing.Api.Controllers;

[ApiController]
[Route("api/uploads")]
[Authorize]
[Produces("application/json")]
public class UploadsController : ControllerBase
{
    private readonly IProcessingService _processingService;

    public UploadsController(IProcessingService processingService)
    {
        _processingService = processingService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(CsvUploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CsvUploadErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(CsvUploadErrorResponse), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(CsvUploadErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(new CsvUploadErrorResponse(0, "Failed", "A CSV file is required in the 'file' form field."));
        }

        await using var uploadStream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await uploadStream.CopyToAsync(buffer, cancellationToken);

        var result = await _processingService.ProcessAsync(
            new CsvUpload(file.FileName, file.Length, buffer.ToArray()),
            cancellationToken);

        if (!result.Succeeded)
        {
            return StatusCode(
                result.ErrorStatusCode,
                new CsvUploadErrorResponse(result.Run.Id, "Failed", result.ErrorMessage ?? "The file could not be processed."));
        }

        var response = new CsvUploadResponse(
            result.Run.Id,
            result.Run.FileName,
            result.Run.Status.ToString(),
            result.Run.RowCount,
            result.Run.ColumnCount,
            result.Run.NumericColumnCount,
            result.Run.UploadedAt,
            result.Run.CompletedAt,
            result.Run.Aggregates.ToResponses());

        return Created($"/api/history/{result.Run.Id}", response);
    }
}
