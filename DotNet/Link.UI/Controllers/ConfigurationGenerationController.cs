using Automation.UI.Models;
using Automation.UI.Services.ConfigurationGeneration;
using Link.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Link.UI.Controllers;

[Route("Automation/ConfigurationGeneration/[action]")]
public class ConfigurationGenerationController : Controller
{
    private readonly LinkAutomationEngineStatus _engine;
    private readonly IServiceProvider _services;
    private BundleConfigurationGenerationService? _generator;

    public ConfigurationGenerationController(LinkAutomationEngineStatus engine, IServiceProvider services)
    {
        _engine = engine;
        _services = services;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        _generator = _services.GetService<BundleConfigurationGenerationService>();
        if (!_engine.Ready || _generator is null)
        {
            context.Result = new ContentResult
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
                Content = _engine.Message ?? AutomationRunReader.NotConfiguredMessage,
                ContentType = "text/plain; charset=utf-8"
            };
            return;
        }

        base.OnActionExecuting(context);
    }

    private BundleConfigurationGenerationService Generator => _generator!;
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeBundleConfigurationRequest request, CancellationToken ct)
    {
        try
        {
            var body = request ?? new AnalyzeBundleConfigurationRequest();
            if (body.RefineOrmId == Guid.Empty) body.RefineOrmId = null;
            if (body.RefineSuiteId == Guid.Empty) body.RefineSuiteId = null;
            var proposal = await Generator.AnalyzeAsync(body, ct);
            return Json(proposal);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyOrm([FromBody] ApplyGeneratedOrmRequest request, CancellationToken ct)
    {
        try
        {
            var saved = await Generator.ApplyOrmAsync(request ?? new ApplyGeneratedOrmRequest(), ct);
            return Json(new { id = saved.Id, name = saved.Name });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyNormalization([FromBody] ApplyGeneratedNormalizationRequest request, CancellationToken ct)
    {
        try
        {
            var saved = await Generator.ApplyNormalizationAsync(request ?? new ApplyGeneratedNormalizationRequest(), ct);
            return Json(new { id = saved.Id, name = saved.Name });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
