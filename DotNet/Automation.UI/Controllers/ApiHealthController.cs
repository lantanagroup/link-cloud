using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using Automation.UI.Services.ApiHealth.TestSuites;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Automation.Link.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace Automation.UI.Controllers;

public class ApiHealthController(
    ApiEndpointRegistry registry,
    ApiHealthTestExecutor executor,
    ApiHealthExecutionRunManager runManager,
    IApiHealthSeedOrchestrator seedOrchestrator,
    IApiHealthSeedContextAccessor seedContext,
    IApiHealthRunStore store,
    IOptions<AutomationConfig> automationConfig,
    ILogger<ApiHealthController> logger) : Controller
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    /// <summary>
    /// Main API Health dashboard page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var endpoints = registry.GetAll();
        var keys = endpoints.Where(e => !e.IsInformational).Select(e => e.Key).ToList();
        var latestResults = await store.GetLatestResultsAsync(keys, ct);

        var groups = endpoints
            .GroupBy(e => e.ServiceName)
            .Select(g => new ServiceEndpointGroup
            {
                ServiceName = g.Key,
                Endpoints = g.Select(e => new EndpointViewModel
                {
                    Definition = e,
                    LastResult = e.IsInformational ? null : latestResults.GetValueOrDefault(e.Key)
                }).ToList()
            })
            .OrderBy(g => g.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var vm = new ApiHealthDashboardViewModel { Services = groups, GrafanaBaseUrl = automationConfig.Value.GrafanaBaseUrl };
        return View(vm);
    }

    /// <summary>
    /// Run a single endpoint test. Returns the updated row partial.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunEndpoint([FromBody] RunEndpointRequest request, CancellationToken ct)
    {
        var endpoint = registry.GetAll().FirstOrDefault(e => e.Key == request.EndpointKey);
        if (endpoint == null) return NotFound(new { error = "Endpoint not found." });
        if (endpoint.IsInformational) return BadRequest(new { error = "This endpoint is informational only and cannot be run." });

        var suite = registry.FindSuiteForEndpoint(endpoint.Key);
        var requirements = suite?.GetSeedRequirements() ?? [];

        var seedSession = await seedOrchestrator.BeginServiceAsync(endpoint.ServiceName, requirements, ct);
        if (!seedSession.Success)
        {
            var problem = new ProblemDetails
            {
                Title = "API Health seeding failed.",
                Detail = seedSession.Error ?? "Seeding failed.",
                Status = StatusCodes.Status500InternalServerError
            };
            problem.Extensions["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            return StatusCode(StatusCodes.Status500InternalServerError, problem);
        }

        if (requirements.Contains(ApiHealthSeedRequirement.ReportSchedule) && seedSession.Report == null)
        {
            var problem = new ProblemDetails
            {
                Title = "API Health seeding contract violation.",
                Detail = "Seeding contract violation: ReportSchedule was required but no report seed payload was produced.",
                Status = StatusCodes.Status500InternalServerError
            };
            problem.Extensions["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            return StatusCode(StatusCodes.Status500InternalServerError, problem);
        }

        try
        {
            seedContext.Current = seedSession;
            var result = await executor.RunEndpointTestAsync(endpoint, ct);
            return Json(result);
        }
        finally
        {
            await seedOrchestrator.EndAsync(seedSession, ct);
        }
    }

    /// <summary>
    /// Run all endpoints for a specific service, streaming results via SSE as each step completes.
    /// </summary>
    [HttpGet]
    public async Task RunServiceStream(string serviceName, CancellationToken ct)
    {
        var runId = await runManager.StartServiceAsync(serviceName);
        await StreamRunAsync(runId, ct);
    }

    [HttpGet]
    public async Task RunStream(Guid runId, CancellationToken ct)
    {
        await StreamRunAsync(runId, ct);
    }

    [HttpGet]
    public IActionResult ActiveRun()
    {
        var activeRun = runManager.GetActiveRun();
        return Json(activeRun);
    }

    /// <summary>
    /// Run all registered endpoints across all services, streaming results via SSE.
    /// Services run sequentially (to avoid overwhelming backends), but results stream per-step.
    /// </summary>
    [HttpGet]
    public async Task RunAllStream(CancellationToken ct)
    {
        var runId = await runManager.StartAllAsync();
        await StreamRunAsync(runId, ct);
    }

    private async Task StreamRunAsync(Guid runId, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        long afterSequence = 0;
        try
        {
            while (true)
            {
                if (!runManager.TryGetRun(runId, out var runInfo))
                {
                    await Response.WriteAsync("event: phase\ndata: {\"phase\":\"Failed\",\"scope\":\"All\",\"message\":\"Run not found.\",\"isError\":true}\n\n", ct);
                    await Response.WriteAsync("event: done\ndata: {}\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                    return;
                }

                var events = runManager.GetEventsSince(runId, afterSequence);
                foreach (var evt in events)
                {
                    await Response.WriteAsync($"event: {evt.EventName}\ndata: {evt.Data}\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                    afterSequence = evt.Sequence;
                }

                if (runInfo.Completed && events.Count == 0)
                {
                    await Response.WriteAsync("event: done\ndata: {}\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                    return;
                }

                await Task.Delay(500, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected; run continues in background.
        }
    }

    /// <summary>
    /// Get paged history for a specific endpoint.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> History(string endpointKey, int pageNumber = DefaultPageNumber, int pageSize = DefaultPageSize, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(endpointKey)) return BadRequest("endpointKey is required.");

        if (pageNumber < DefaultPageNumber)
            return BadRequest($"pageNumber must be >= {DefaultPageNumber}.");

        if (pageSize is < 1 or > MaxPageSize)
            return BadRequest($"pageSize must be between 1 and {MaxPageSize}.");

        var history = await store.GetHistoryAsync(endpointKey, pageNumber, pageSize, ct);
        return Json(history);
    }

}

public sealed class RunEndpointRequest
{
    public string EndpointKey { get; set; } = string.Empty;
}
