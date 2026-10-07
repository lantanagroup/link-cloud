using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth;
using Automation.UI.Services.Persistence;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

public sealed class ApiHealthController : Controller
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly LinkAutomationEngineStatus _engine;
    private readonly ApiEndpointRegistry? _registry;
    private readonly ApiHealthExecutionRunManager? _runManager;
    private readonly IApiHealthRunStore? _store;

    public ApiHealthController(LinkAutomationEngineStatus engine, IServiceProvider services)
    {
        _engine = engine;
        _registry = services.GetService<ApiEndpointRegistry>();
        _runManager = services.GetService<ApiHealthExecutionRunManager>();
        _store = services.GetService<IApiHealthRunStore>();
    }

    private bool Ready => _engine.Ready && _registry is not null && _runManager is not null && _store is not null;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "API Health";
        ViewData["AutomationSection"] = "api-health";
        if (!Ready)
        {
            ViewBag.EngineMessage = _engine.Message ?? AutomationRunReader.NotConfiguredMessage;
            return View(new ApiHealthDashboardViewModel());
        }

        var endpoints = _registry!.GetAll();
        var keys = endpoints.Where(e => !e.IsInformational).Select(e => e.Key).ToList();
        var activeExecution = await _store!.GetActiveExecutionRunStatusAsync(ct);
        var latestRunContext = await _store.GetLatestRunContextAsync(ct);
        var latestServiceResults = await _store.GetLatestResultsByServiceAsync(keys, ct);
        var selectedRunId = activeExecution?.RunId ?? latestRunContext?.RunId;
        var selectedRunMode = activeExecution?.RunMode ?? latestRunContext?.RunMode;
        var selectedServiceName = activeExecution?.ServiceName ?? latestRunContext?.ServiceName;
        var selectedRunResults = selectedRunId is Guid runId
            ? await _store.GetLatestResultsForRunAsync(runId, keys, ct)
            : new Dictionary<string, ApiTestRunResult>(StringComparer.Ordinal);

        var selectedIsAll = string.Equals(selectedRunMode, "All", StringComparison.OrdinalIgnoreCase);

        var groups = endpoints
            .GroupBy(e => e.ServiceName)
            .Select(g => new ServiceEndpointGroup
            {
                ServiceName = g.Key,
                IsIncludedInLatestRun = selectedRunId == null
                    || selectedIsAll
                    || string.Equals(selectedServiceName, g.Key, StringComparison.OrdinalIgnoreCase),
                Endpoints = g.Select(e => new EndpointViewModel
                {
                    Definition = e,
                    LastResult = e.IsInformational
                        ? null
                        : (selectedRunId == null
                            ? latestServiceResults.GetValueOrDefault(e.Key)
                            : (selectedIsAll || string.Equals(selectedServiceName, e.ServiceName, StringComparison.OrdinalIgnoreCase)
                                ? selectedRunResults.GetValueOrDefault(e.Key)
                                : latestServiceResults.GetValueOrDefault(e.Key)))
                }).ToList()
            })
            .OrderBy(g => g.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return View(new ApiHealthDashboardViewModel
        {
            Services = groups,
            HasActiveRun = activeExecution != null,
            LatestRunMode = selectedRunMode,
            LatestRunServiceName = selectedServiceName
        });
    }

    [HttpGet]
    public async Task RunServiceStream(string serviceName, CancellationToken ct)
    {
        if (!Ready)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        serviceName = serviceName.Sanitize().Trim();
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var runId = await _runManager!.StartServiceAsync(serviceName);
        await StreamRunAsync(runId, ct);
    }

    [HttpGet]
    public async Task RunStream(Guid runId, CancellationToken ct)
    {
        if (!Ready)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        await StreamRunAsync(runId, ct);
    }

    [HttpGet]
    public async Task<IActionResult> ActiveRun(CancellationToken ct)
    {
        if (!Ready)
            return Json(null);

        var activeRun = await _runManager!.GetActiveRunAsync(ct);
        return Json(activeRun);
    }

    [HttpGet]
    public async Task RunAllStream(CancellationToken ct)
    {
        if (!Ready)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        var runId = await _runManager!.StartAllAsync();
        await StreamRunAsync(runId, ct);
    }

    [HttpGet]
    public async Task<IActionResult> History(
        string endpointKey,
        int pageNumber = DefaultPageNumber,
        int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        if (!Ready)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        endpointKey = endpointKey.Sanitize().Trim();
        if (string.IsNullOrWhiteSpace(endpointKey))
            return BadRequest("endpointKey is required.");

        if (pageNumber < DefaultPageNumber)
            return BadRequest($"pageNumber must be >= {DefaultPageNumber}.");

        if (pageSize is < 1 or > MaxPageSize)
            return BadRequest($"pageSize must be between 1 and {MaxPageSize}.");

        var history = await _store!.GetHistoryAsync(endpointKey, pageNumber, pageSize, ct);
        return Json(history);
    }

    private async Task StreamRunAsync(Guid runId, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.Headers["Connection"] = "keep-alive";

        long afterSequence = 0;
        var lastWriteAt = DateTimeOffset.UtcNow;
        var heartbeatInterval = TimeSpan.FromSeconds(15);
        try
        {
            while (true)
            {
                if (!_runManager!.TryGetRun(runId, out var runInfo))
                {
                    await Response.WriteAsync("event: phase\ndata: {\"phase\":\"Failed\",\"scope\":\"All\",\"message\":\"Run not found.\",\"isError\":true}\n\n", ct);
                    await Response.WriteAsync("event: done\ndata: {}\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                    return;
                }

                var events = _runManager.GetEventsSince(runId, afterSequence);
                foreach (var evt in events)
                {
                    await Response.WriteAsync($"event: {evt.EventName}\ndata: {evt.Data}\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                    afterSequence = evt.Sequence;
                    lastWriteAt = DateTimeOffset.UtcNow;
                }

                if (runInfo.Completed && events.Count == 0)
                {
                    await Response.WriteAsync("event: done\ndata: {}\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                    return;
                }

                if (events.Count == 0 && DateTimeOffset.UtcNow - lastWriteAt >= heartbeatInterval)
                {
                    await Response.WriteAsync(": keep-alive\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                    lastWriteAt = DateTimeOffset.UtcNow;
                }

                await Task.Delay(500, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected; the run continues in the background.
        }
    }
}
