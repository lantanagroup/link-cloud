using Link.UI.Services.ReportBatches;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers.Api;

/// <summary>
/// Starts a batch of report runs, then reports and cancels that batch.
/// Callers poll <see cref="Get"/> until the batch is completed or cancelled.
/// </summary>
[ApiController]
[Route("api/report-batches")]
[Authorize(Policy = "ApiBearerPolicy")]
public sealed class ReportBatchesApiController(IServiceProvider services) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ReportBatchResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Start([FromBody] StartReportBatchRequest? request, CancellationToken cancellationToken)
    {
        var dispatcher = services.GetService<ReportBatchDispatcher>();
        if (dispatcher is null)
            return Unavailable();

        try
        {
            var batch = await dispatcher.StartBatchAsync(
                request?.EhrSystem,
                request?.ScenarioIds,
                request?.MaxConcurrency,
                cancellationToken);
            return AcceptedAtAction(nameof(Get), new { batchId = batch.BatchId }, ReportBatchResponse.From(batch));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid batch", detail: ex.Message);
        }
    }

    [HttpGet("{batchId:guid}")]
    [ProducesResponseType(typeof(ReportBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(Guid batchId, CancellationToken cancellationToken)
    {
        var dispatcher = services.GetService<ReportBatchDispatcher>();
        if (dispatcher is null)
            return Unavailable();

        var batch = await dispatcher.GetBatchAsync(batchId, cancellationToken);
        if (batch is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Batch not found",
                detail: $"Batch {batchId} was not found.");
        }

        return Ok(ReportBatchResponse.From(batch));
    }

    [HttpPost("{batchId:guid}/cancel")]
    [ProducesResponseType(typeof(ReportBatchResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Cancel(Guid batchId, CancellationToken cancellationToken)
    {
        var dispatcher = services.GetService<ReportBatchDispatcher>();
        if (dispatcher is null)
            return Unavailable();

        var batch = await dispatcher.CancelBatchAsync(batchId, cancellationToken);
        if (batch is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Batch not found",
                detail: $"Batch {batchId} was not found.");
        }

        return Accepted(ReportBatchResponse.From(batch));
    }

    private ObjectResult Unavailable()
        => Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Report batches are unavailable",
            detail: "The automation engine is not ready.");
}

public sealed class StartReportBatchRequest
{
    public string? EhrSystem { get; set; }

    public List<Guid>? ScenarioIds { get; set; }

    public int? MaxConcurrency { get; set; }
}

public sealed class ReportBatchResponse
{
    public Guid BatchId { get; set; }

    public string EhrSystem { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int MaxConcurrency { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public List<ReportBatchItemResponse> Items { get; set; } = [];

    public static ReportBatchResponse From(ReportBatchDocument batch)
        => new()
        {
            BatchId = batch.BatchId,
            EhrSystem = batch.EhrSystem,
            Status = batch.Status,
            MaxConcurrency = batch.MaxConcurrency,
            CreatedAt = batch.CreatedAt,
            StartedAt = batch.StartedAt,
            FinishedAt = batch.FinishedAt,
            Items = batch.Items
                .OrderBy(item => item.Ordinal)
                .Select(item => new ReportBatchItemResponse
                {
                    Ordinal = item.Ordinal,
                    ScenarioId = item.ScenarioId,
                    ScenarioName = item.ScenarioName,
                    RunId = item.RunId,
                    Status = item.Status,
                    Error = item.Error,
                    IsMetricsRun = item.IsMetricsRun
                })
                .ToList()
        };
}

public sealed class ReportBatchItemResponse
{
    public int Ordinal { get; set; }

    public Guid ScenarioId { get; set; }

    public string ScenarioName { get; set; } = string.Empty;

    public Guid? RunId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Error { get; set; }

    public bool IsMetricsRun { get; set; }
}
