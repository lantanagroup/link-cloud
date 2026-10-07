using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using Link.UI.Models;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Link.UI.Controllers;

[Route("Automation")]
public sealed partial class AutomationController : Controller
{
    private readonly AutomationRunReader _runs;
    private readonly LinkAutomationEngineStatus _engine;
    private readonly IServiceProvider _services;
    private readonly ILogger<AutomationController> _logger;
    private readonly IAutomationRunManager? _manager;
    private readonly IScenarioStore? _scenarios;

    public AutomationController(
        AutomationRunReader runs,
        LinkAutomationEngineStatus engine,
        IServiceProvider services,
        ILogger<AutomationController> logger)
    {
        _runs = runs;
        _engine = engine;
        _services = services;
        _logger = logger;
        _manager = services.GetService<IAutomationRunManager>();
        _scenarios = services.GetService<IScenarioStore>();
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(AutomationRunQuery query, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Automation";
        ViewData["AutomationSection"] = "runs";
        var page = await _runs.LoadDashboardAsync(query, cancellationToken);
        page.EngineReady = _engine.Ready && _manager is not null;
        if (page.EngineReady)
            await FillScenarioEditorCatalogsAsync(cancellationToken);
        return View(page);
    }

    [HttpGet("recent")]
    public async Task<IActionResult> Recent(AutomationRunQuery query, CancellationToken cancellationToken)
    {
        return PartialView("_RecentRuns", await _runs.LoadDashboardAsync(query, cancellationToken));
    }

    [HttpGet("data")]
    public async Task<IActionResult> Data(AutomationRunQuery query, CancellationToken cancellationToken)
    {
        return Json(await _runs.LoadDashboardAsync(query, cancellationToken));
    }

    [HttpGet("Runs/new")]
    public async Task<IActionResult> New(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "New automation run";
        ViewData["AutomationSection"] = "runs";
        return View(await BuildNewPageAsync(null, null, null, cancellationToken));
    }

    [HttpPost("Runs/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(string? choice, string? runName, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "New automation run";
        ViewData["AutomationSection"] = "runs";

        if (!_engine.Ready || _manager is null)
        {
            return View(await BuildNewPageAsync(
                choice,
                runName,
                _engine.Message ?? AutomationRunReader.NotConfiguredMessage,
                cancellationToken));
        }

        if (!LinkAutomationStartRules.TryParseChoice(choice, out var kind, out var scenarioId, out var choiceError))
            return View(await BuildNewPageAsync(choice, runName, choiceError, cancellationToken));

        if (!LinkAutomationStartRules.TryRunName(runName, out var name, out var nameError))
            return View(await BuildNewPageAsync(choice, runName, nameError, cancellationToken));

        StartScenarioRequest request;
        if (scenarioId != Guid.Empty)
        {
            if (_scenarios is null)
            {
                return View(await BuildNewPageAsync(
                    choice,
                    runName,
                    AutomationRunReader.NotConfiguredMessage,
                    cancellationToken));
            }

            var scenario = await _scenarios.GetByIdAsync(scenarioId, cancellationToken);
            if (scenario is null)
            {
                return View(await BuildNewPageAsync(
                    choice,
                    runName,
                    "That scenario is not stored.",
                    cancellationToken));
            }

            request = StartScenarioRequest.FromScenario(scenario);
            if (!string.IsNullOrEmpty(name))
                request.ScenarioName = name;
        }
        else
        {
            request = LinkAutomationStartRules.BuildKindRequest(kind!.Value, name);
        }

        try
        {
            var runId = await _manager.StartAsync(request, cancellationToken);
            return RedirectToAction(nameof(Run), new { id = runId });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return View(await BuildNewPageAsync(
                choice,
                runName,
                LinkAutomationStartRules.ExplainStartFailure(ex),
                cancellationToken));
        }
    }

    [HttpGet("Runs/{id:guid}")]
    public async Task<IActionResult> Run(Guid id, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Automation run";
        ViewData["AutomationSection"] = "runs";
        var page = await _runs.LoadRunAsync(id, cancellationToken);
        if (page.Found && _manager is not null)
        {
            page.Detail = await _manager.GetRunForDisplayAsync(id, cancellationToken);
            if (page.Detail is not null)
                await FillRunDetailAsync(page.Detail, cancellationToken);
        }

        return View(page);
    }

    [HttpGet("Runs/{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, CancellationToken cancellationToken)
    {
        return Json(await _runs.LoadRunAsync(id, cancellationToken));
    }

    private async Task<AutomationNewRunPage> BuildNewPageAsync(
        string? choice,
        string? runName,
        string? error,
        CancellationToken cancellationToken)
    {
        var listed = await _runs.ListScenariosAsync(cancellationToken);
        var message = !_engine.Ready
            ? _engine.Message
            : listed.Message;

        return new AutomationNewRunPage
        {
            EngineReady = _engine.Ready && _manager is not null,
            Message = message,
            Error = error,
            Choice = choice,
            RunName = runName,
            ScenariosTruncated = listed.Truncated,
            BuiltIn = LinkAutomationStartRules.BuiltInChoices,
            Scenarios = LinkAutomationStartRules.OrderScenarios(listed.Scenarios)
        };
    }
}
