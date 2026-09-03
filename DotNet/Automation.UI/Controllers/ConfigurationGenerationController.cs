using Automation.UI.Models;
using Automation.UI.Services.ConfigurationGeneration;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class ConfigurationGenerationController(BundleConfigurationGenerationService generator) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeBundleConfigurationRequest request, CancellationToken ct)
    {
        try
        {
            var body = request ?? new AnalyzeBundleConfigurationRequest();
            if (body.RefineOrmId == Guid.Empty) body.RefineOrmId = null;
            if (body.RefineSuiteId == Guid.Empty) body.RefineSuiteId = null;
            var proposal = await generator.AnalyzeAsync(body, ct);
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
            var saved = await generator.ApplyOrmAsync(request ?? new ApplyGeneratedOrmRequest(), ct);
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
            var saved = await generator.ApplyNormalizationAsync(request ?? new ApplyGeneratedNormalizationRequest(), ct);
            return Json(new { id = saved.Id, name = saved.Name });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
