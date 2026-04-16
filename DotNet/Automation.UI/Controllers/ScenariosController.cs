using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class ScenariosController(IScenarioStore scenarioStore, IQueryPlanTemplateStore queryPlanTemplateStore) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var scenarios = await scenarioStore.GetAllAsync(ct);
        ViewBag.QueryPlanTemplates = await queryPlanTemplateStore.GetAllAsync(ct);
        return View(scenarios);
    }

    [HttpGet]
    public async Task<IActionResult> GetJson(Guid id, CancellationToken ct)
    {
        var scenario = await scenarioStore.GetByIdAsync(id, ct);
        if (scenario == null) return NotFound();
        return Json(scenario);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveInline([FromBody] TestScenarioDefinition model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            return BadRequest("Scenario name is required.");

        var existing = await scenarioStore.GetByIdAsync(model.Id, ct);
        if (existing is { IsSystemScenario: true })
            return Forbid();

        model.IsSystemScenario = false;
        model.UpdatedAt = DateTimeOffset.UtcNow;

        if (model.ResourcesPerPatientMax < model.ResourcesPerPatientMin)
            model.ResourcesPerPatientMax = model.ResourcesPerPatientMin;

        await scenarioStore.UpsertAsync(model, ct);
        return Json(new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInline([FromBody] IdRequest request, CancellationToken ct)
    {
        var scenario = await scenarioStore.GetByIdAsync(request.Id, ct);
        if (scenario == null) return NotFound();
        if (scenario.IsSystemScenario) return Forbid();

        await scenarioStore.DeleteAsync(request.Id, ct);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloneInline([FromBody] IdRequest request, CancellationToken ct)
    {
        var source = await scenarioStore.GetByIdAsync(request.Id, ct);
        if (source == null) return NotFound();

        var clone = new TestScenarioDefinition
        {
            Id = Guid.NewGuid(),
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            IsSystemScenario = false,
            ReportMethod = source.ReportMethod,
            SelectedMeasures = [.. source.SelectedMeasures],
            Seed = source.Seed,
            PatientCount = source.PatientCount,
            ResourcesPerPatientMin = source.ResourcesPerPatientMin,
            ResourcesPerPatientMax = source.ResourcesPerPatientMax,
            PatientPrefix = source.PatientPrefix,
            UseMeasureEligibilityProfiles = source.UseMeasureEligibilityProfiles,
            PatientProfiles = [.. source.PatientProfiles],
            PatientCohorts = source.PatientCohorts
                .Select(c => new PatientCohortDefinition
                {
                    PatientCount = c.PatientCount,
                    MeasureEligibilities = new(c.MeasureEligibilities),
                    EligibleClinicalScenarioIds = [.. c.EligibleClinicalScenarioIds],
                    ResourcesPerPatientMin = c.ResourcesPerPatientMin,
                    ResourcesPerPatientMax = c.ResourcesPerPatientMax
                })
                .ToList(),
            SelectedClinicalScenarioIds = [.. source.SelectedClinicalScenarioIds],
            DischargeCount = source.DischargeCount,
            DischargeQualifyingCount = source.DischargeQualifyingCount,
            DischargeNonQualifyingCount = source.DischargeNonQualifyingCount,
            QueryPlanTemplateId = source.QueryPlanTemplateId,
            CleanupServiceData = source.CleanupServiceData,
            CleanupFhirData = source.CleanupFhirData,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await scenarioStore.UpsertAsync(clone, ct);
        return Json(new { id = clone.Id });
    }

    public sealed class IdRequest
    {
        public Guid Id { get; set; }
    }
}
