using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers.Api;

/// <summary>
/// Service-to-service API for triggering automation runs from outside the UI
/// (e.g. from the deployment pipeline as a post-deploy smoke test).
///
/// Notifications (Slack, etc.) are intentionally NOT handled in this app: the
/// caller polls <see cref="GetStatus"/> and is responsible for whatever
/// downstream messaging it wants to do with the result. This keeps Automation.UI
/// free of webhook configuration and credentials.
/// </summary>
[ApiController]
[Route("api/runs")]
[Authorize(Policy = "ApiBearerPolicy")]
public sealed class AutomationRunsApiController(
    IAutomationRunManager runManager,
    IScenarioStore scenarioStore,
    ILogger<AutomationRunsApiController> logger) : ControllerBase
{
    /// <summary>
    /// Starts a saved scenario by id and returns immediately with the new run id.
    /// The run continues asynchronously in the background; callers should poll
    /// <see cref="GetStatus"/> until the run reaches a terminal state.
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartScenarioApiRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.ScenarioId == Guid.Empty)
            return BadRequest(new { error = "scenarioId is required." });

        var scenario = await scenarioStore.GetByIdAsync(request.ScenarioId, cancellationToken);
        if (scenario == null)
            return NotFound(new { error = $"Scenario {request.ScenarioId} not found." });

        var startRequest = StartScenarioRequest.FromScenario(scenario);

        Guid runId;
        try
        {
            runId = await runManager.StartAsync(startRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start scenario {ScenarioId} via API.", request.ScenarioId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }

        return Accepted(new
        {
            runId,
            scenarioId = scenario.Id,
            scenarioName = scenario.Name,
            source = request.Source,
        });
    }

    /// <summary>
    /// Returns lightweight status for a run. Designed for polling by external
    /// callers (deployment pipelines) that need to know when a run has reached a
    /// terminal state without pulling the full <see cref="AutomationRunSummary"/>.
    /// </summary>
    [HttpGet("{runId:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid runId, CancellationToken cancellationToken)
    {
        var summary = await runManager.GetRunAsync(runId, cancellationToken);
        if (summary == null)
            return NotFound(new { error = $"Run {runId} not found." });

        var isTerminal = summary.Status.IsTerminal();

        return Ok(new RunStatusResponse
        {
            RunId = summary.RunId,
            RunName = summary.RunName,
            Status = summary.Status.ToString(),
            IsTerminal = isTerminal,
            CreatedAt = summary.CreatedAt,
            StartedAt = summary.StartedAt,
            FinishedAt = summary.FinishedAt,
            Duration = summary.Duration,
            Error = summary.Error,
        });
    }

    public sealed class LivePatientEventRequest
    {
        public string? PatientId { get; set; }
        public string? Notes { get; set; }
        public string? Source { get; set; }
    }

    [HttpPost("{runId:guid}/events/admit")]
    public async Task<IActionResult> Admit(Guid runId, [FromBody] LivePatientEventRequest? request, CancellationToken cancellationToken)
    {
        return await ExecuteLiveInjectAsync(
            runId,
            () => runManager.InjectAdmitAsync(
                runId,
                request?.PatientId,
                string.IsNullOrWhiteSpace(request?.Source) ? "API" : request!.Source!.Trim(),
                request?.Notes,
                cancellationToken),
            cancellationToken);
    }

    [HttpPost("{runId:guid}/events/discharge")]
    public async Task<IActionResult> Discharge(Guid runId, [FromBody] LivePatientEventRequest? request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.PatientId))
            return BadRequest(new { error = "patientId is required." });

        return await ExecuteLiveInjectAsync(
            runId,
            () => runManager.InjectDischargeAsync(
                runId,
                request.PatientId,
                string.IsNullOrWhiteSpace(request.Source) ? "API" : request.Source.Trim(),
                request.Notes,
                cancellationToken),
            cancellationToken);
    }

    [HttpGet("{runId:guid}/events")]
    public async Task<IActionResult> GetEvents(Guid runId, CancellationToken cancellationToken)
    {
        if (await runManager.GetRunAsync(runId, cancellationToken) == null)
            return NotFound(new { error = $"Run {runId} not found." });

        var events = await runManager.GetLiveEventsAsync(runId, cancellationToken);
        return Ok(events);
    }

    [HttpGet("{runId:guid}/patient-state")]
    public async Task<IActionResult> GetPatientState(Guid runId, CancellationToken cancellationToken)
    {
        if (await runManager.GetRunAsync(runId, cancellationToken) == null)
            return NotFound(new { error = $"Run {runId} not found." });

        var state = await runManager.GetLivePatientStateAsync(runId, cancellationToken);
        return Ok(new
        {
            admitted = state.Admitted,
            dischargedDuringWindow = state.DischargedDuringWindow,
            expectedPopulation = state.ExpectedPopulation,
            acceptingInjections = state.AcceptingInjections,
            windowStartUtc = state.WindowStartUtc,
            windowEndUtc = state.WindowEndUtc,
            reportGenerationTimeUtc = state.ReportGenerationTimeUtc
        });
    }

    private async Task<IActionResult> ExecuteLiveInjectAsync(
        Guid runId,
        Func<Task<PatientStateEvent>> action,
        CancellationToken cancellationToken)
    {
        if (await runManager.GetRunAsync(runId, cancellationToken) == null)
            return NotFound(new { error = $"Run {runId} not found." });

        try
        {
            var evt = await action();
            return Ok(evt);
        }
        catch (LiveInjectionException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
    }

    public sealed class StartScenarioApiRequest
    {
        /// <summary>Saved scenario id to launch.</summary>
        public Guid ScenarioId { get; set; }

        /// <summary>
        /// Free-form label echoed back in the response so the caller can confirm
        /// which trigger started the run (e.g. <c>"Test deploy pipeline"</c>).
        /// Optional.
        /// </summary>
        public string? Source { get; set; }
    }

    public sealed class RunStatusResponse
    {
        public Guid RunId { get; set; }
        public string RunName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsTerminal { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public string? Duration { get; set; }
        public string? Error { get; set; }
    }
}
