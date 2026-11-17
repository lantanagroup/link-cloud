using Link.UI.Services.ReportBatches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers.Api;

/// <summary>
/// The saved concurrency cap for report batches. A change applies to batches
/// dispatched after the change. It is not a config-file or environment setting.
/// </summary>
[ApiController]
[Route("api/report-batch-settings")]
[Authorize(Policy = "ApiBearerPolicy")]
public sealed class ReportBatchSettingsApiController(IServiceProvider services) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ReportBatchSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var store = services.GetService<IReportBatchSettingsStore>();
        if (store is null)
            return Unavailable();

        var cap = await store.GetMaxConcurrencyAsync(cancellationToken);
        return Ok(new ReportBatchSettingsResponse { MaxConcurrency = cap });
    }

    [HttpPut]
    [ProducesResponseType(typeof(ReportBatchSettingsResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Put([FromBody] ReportBatchSettingsRequest? request, CancellationToken cancellationToken)
    {
        var store = services.GetService<IReportBatchSettingsStore>();
        if (store is null)
            return Unavailable();

        if (request is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid concurrency cap",
                detail: ReportBatchConcurrencyPolicy.Validate(0));
        }

        var error = ReportBatchConcurrencyPolicy.Validate(request.MaxConcurrency);
        if (error != null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid concurrency cap",
                detail: error);
        }

        await store.SaveMaxConcurrencyAsync(request.MaxConcurrency, cancellationToken);
        return Accepted(new ReportBatchSettingsResponse { MaxConcurrency = request.MaxConcurrency });
    }

    private ObjectResult Unavailable()
        => Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Report batch settings are unavailable",
            detail: "The automation engine is not ready.");
}

public sealed class ReportBatchSettingsRequest
{
    public int MaxConcurrency { get; set; }
}

public sealed class ReportBatchSettingsResponse
{
    public int MaxConcurrency { get; set; }
}
