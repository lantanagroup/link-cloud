using System.Text.Json;
using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers.Api;

[ApiController]
[Route("api/api-health-runs")]
public sealed class ApiHealthRunsApiController(ApiHealthExecutionRunManager runManager) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [HttpPost("start-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartAll(CancellationToken cancellationToken)
    {
        var runId = await runManager.StartAllAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return CreatedAtAction(nameof(GetStatus), new { runId }, new
        {
            runId,
            scope = "All"
        });
    }

    [HttpGet("{runId:guid}/status")]
    public IActionResult GetStatus(Guid runId)
    {
        if (!runManager.TryGetRun(runId, out var runInfo))
            return NotFound(new { error = $"Run {runId} not found." });

        var results = GetRunResults(runId);
        var failedResults = results.Where(r => !r.Passed && !r.Skipped).ToList();
        var status = !runInfo.Completed
            ? "Running"
            : runInfo.Failed || failedResults.Count > 0
                ? "Failed"
                : "Succeeded";

        return Ok(new RunAllStatusResponse
        {
            RunId = runInfo.RunId,
            Scope = runInfo.Scope,
            ServiceName = runInfo.ServiceName,
            Status = status,
            IsTerminal = runInfo.Completed,
            StartedAt = runInfo.StartedAt,
            FinishedAt = runInfo.FinishedAt,
            Duration = runInfo.FinishedAt.HasValue ? (runInfo.FinishedAt.Value - runInfo.StartedAt).ToString() : null,
            Error = runInfo.Error,
            TotalEndpoints = results.Count,
            PassedEndpoints = results.Count(r => r.Passed),
            FailedEndpoints = failedResults.Count,
            SkippedEndpoints = results.Count(r => r.Skipped),
            FailedResults = failedResults.Select(ToFailure).ToList()
        });
    }

    [HttpGet("{runId:guid}/results")]
    public IActionResult GetResults(Guid runId)
    {
        if (!runManager.TryGetRun(runId, out _))
            return NotFound(new { error = $"Run {runId} not found." });

        var results = GetRunResults(runId)
            .OrderBy(r => r.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.EndpointName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(results);
    }

    private IReadOnlyList<ApiTestRunResult> GetRunResults(Guid runId)
    {
        var events = runManager.GetEventsSince(runId, afterSequence: 0);
        var results = new List<ApiTestRunResult>();

        foreach (var evt in events.Where(e => string.Equals(e.EventName, "result", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var result = JsonSerializer.Deserialize<ApiTestRunResult>(evt.Data, JsonOptions);
                if (result != null)
                    results.Add(result);
            }
            catch (JsonException)
            {
                // Ignore malformed event payloads.
            }
        }

        return results;
    }

    private static FailedEndpointResult ToFailure(ApiTestRunResult result) => new()
    {
        EndpointKey = result.EndpointKey,
        ServiceName = result.ServiceName,
        EndpointName = result.EndpointName,
        ErrorMessage = result.ErrorMessage,
        ActualStatusCode = result.ActualStatusCode,
        ExpectedStatusCode = result.ExpectedStatusCode
    };

    public sealed class RunAllStatusResponse
    {
        public Guid RunId { get; set; }
        public string Scope { get; set; } = string.Empty;
        public string? ServiceName { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsTerminal { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public string? Duration { get; set; }
        public string? Error { get; set; }
        public int TotalEndpoints { get; set; }
        public int PassedEndpoints { get; set; }
        public int FailedEndpoints { get; set; }
        public int SkippedEndpoints { get; set; }
        public IReadOnlyList<FailedEndpointResult> FailedResults { get; set; } = [];
    }

    public sealed class FailedEndpointResult
    {
        public string EndpointKey { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string EndpointName { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public int? ActualStatusCode { get; set; }
        public int ExpectedStatusCode { get; set; }
    }
}
