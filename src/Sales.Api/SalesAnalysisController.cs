using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sales.Api.Security;
using Sales.Application;
using Sales.Domain;

namespace Sales.Api;

[ApiController]
[EnableRateLimiting(UploadLimits.ConcurrencyPolicy)]
[Route("api/sales/analysis")]
public sealed class SalesAnalysisController(AnalyzeSalesFile analyzeSalesFile) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(UploadLimits.MaxRequestBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<SalesAnalysis>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public ActionResult<SalesAnalysis> Analyze(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Problem(statusCode: 400, title: "Invalid sales file", detail: "Upload a non-empty CSV in the file field.");

        if (Request.Form.Files.Count != 1)
            return Problem(statusCode: 400, title: "Invalid sales file", detail: "Upload exactly one CSV file.");
        if (file.Length > UploadLimits.MaxFileBytes)
            return Problem(statusCode: 413, title: "File too large", detail: "The CSV must not exceed 5 MiB.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
            return Problem(statusCode: 400, title: "Invalid sales file", detail: "The uploaded file must have a .csv extension.");

        try
        {
            using var stream = file.OpenReadStream();
            return Ok(analyzeSalesFile.Execute(stream, cancellationToken));
        }
        catch (InvalidSalesFileException exception)
        {
            return Problem(statusCode: 400, title: "Invalid sales file", detail: exception.Message);
        }
        catch (OverflowException)
        {
            return Problem(statusCode: 400, title: "Invalid sales file", detail: "Numeric totals exceed the supported decimal range.");
        }
    }
}
