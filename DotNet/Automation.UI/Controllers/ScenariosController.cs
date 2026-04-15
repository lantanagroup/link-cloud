using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

[Authorize]
public class ScenariosController(IScenarioStore scenarioStore, IQueryPlanTemplateStore queryPlanTemplateStore) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var scenarios = await scenarioStore.GetAllAsync(cancellationToken);
        return View(scenarios);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new TestScenarioDefinition();
        await PopulateSavedScenariosAsync(cancellationToken);
        await PopulateQueryPlanTemplatesAsync(cancellationToken);
        return View("Edit", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var scenario = await scenarioStore.GetByIdAsync(id, cancellationToken);
        if (scenario == null)
            return NotFound();

        if (scenario.IsSystemScenario)
            return RedirectToAction(nameof(Details), new { id });

        ViewBag.InitialScenarioId = id;
        await PopulateSavedScenariosAsync(cancellationToken);
        await PopulateQueryPlanTemplatesAsync(cancellationToken);
        return View(scenario);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var scenario = await scenarioStore.GetByIdAsync(id, cancellationToken);
        if (scenario == null)
            return NotFound();

        await PopulateQueryPlanTemplatesAsync(cancellationToken);
        return View(scenario);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(TestScenarioDefinition model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateQueryPlanTemplatesAsync(cancellationToken);
            return View("Edit", model);
        }

        // Prevent saving over system scenarios.
        var existing = await scenarioStore.GetByIdAsync(model.Id, cancellationToken);
        if (existing is { IsSystemScenario: true })
            return RedirectToAction(nameof(Details), new { id = model.Id });

        model.IsSystemScenario = false;
        model.UpdatedAt = DateTimeOffset.UtcNow;

        // Normalize resource range
        if (model.ResourcesPerPatientMax < model.ResourcesPerPatientMin)
            model.ResourcesPerPatientMax = model.ResourcesPerPatientMin;

        await scenarioStore.UpsertAsync(model, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clone(Guid id, CancellationToken cancellationToken)
    {
        var source = await scenarioStore.GetByIdAsync(id, cancellationToken);
        if (source == null)
            return NotFound();

        var clone = new TestScenarioDefinition
        {
            Id = Guid.NewGuid(),
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            IsSystemScenario = false,
            ReportMethod = source.ReportMethod,
            SelectedMeasures = [..source.SelectedMeasures],
            Seed = source.Seed,
            PatientCount = source.PatientCount,
            ResourcesPerPatientMin = source.ResourcesPerPatientMin,
            ResourcesPerPatientMax = source.ResourcesPerPatientMax,
            PatientPrefix = source.PatientPrefix,
            UseMeasureEligibilityProfiles = source.UseMeasureEligibilityProfiles,
            PatientProfiles = [..source.PatientProfiles],
            PatientCohorts = source.PatientCohorts
                .Select(c => new PatientCohortDefinition
                {
                    PatientCount = c.PatientCount,
                    MeasureEligibilities = new(c.MeasureEligibilities),
                    EligibleClinicalScenarioIds = [..c.EligibleClinicalScenarioIds],
                    ResourcesPerPatientMin = c.ResourcesPerPatientMin,
                    ResourcesPerPatientMax = c.ResourcesPerPatientMax
                })
                .ToList(),
            SelectedClinicalScenarioIds = [..source.SelectedClinicalScenarioIds],
            DischargeCount = source.DischargeCount,
            DischargeQualifyingCount = source.DischargeQualifyingCount,
            DischargeNonQualifyingCount = source.DischargeNonQualifyingCount,
            QueryPlanTemplateId = source.QueryPlanTemplateId,
            CleanupServiceData = source.CleanupServiceData,
            CleanupFhirData = source.CleanupFhirData,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await scenarioStore.UpsertAsync(clone, cancellationToken);
        return RedirectToAction(nameof(Edit), new { id = clone.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var scenario = await scenarioStore.GetByIdAsync(id, cancellationToken);
        if (scenario is { IsSystemScenario: true })
            return RedirectToAction(nameof(Index));

        await scenarioStore.DeleteAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveInline([FromBody] TestScenarioDefinition model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            return BadRequest("Scenario name is required.");

        var existing = await scenarioStore.GetByIdAsync(model.Id, cancellationToken);
        if (existing is { IsSystemScenario: true })
            return Forbid();

        model.IsSystemScenario = false;
        model.UpdatedAt = DateTimeOffset.UtcNow;

        if (model.ResourcesPerPatientMax < model.ResourcesPerPatientMin)
            model.ResourcesPerPatientMax = model.ResourcesPerPatientMin;

        await scenarioStore.UpsertAsync(model, cancellationToken);
        return Json(new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInline([FromBody] DeleteScenarioRequest request, CancellationToken cancellationToken)
    {
        var scenario = await scenarioStore.GetByIdAsync(request.Id, cancellationToken);
        if (scenario == null)
            return NotFound();

        if (scenario.IsSystemScenario)
            return Forbid();

        await scenarioStore.DeleteAsync(request.Id, cancellationToken);
        return Ok();
    }

    public sealed class DeleteScenarioRequest
    {
        public Guid Id { get; set; }
    }

    /// <summary>
    /// Returns scenario JSON for the Runs/Index quick-load feature.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetJson(Guid id, CancellationToken cancellationToken)
    {
        var scenario = await scenarioStore.GetByIdAsync(id, cancellationToken);
        if (scenario == null)
            return NotFound();

        return Json(scenario);
    }

    private async Task PopulateQueryPlanTemplatesAsync(CancellationToken cancellationToken)
    {
        ViewBag.QueryPlanTemplates = await queryPlanTemplateStore.GetAllAsync(cancellationToken);
    }

    private async Task PopulateSavedScenariosAsync(CancellationToken cancellationToken)
    {
        ViewBag.SavedScenarios = await scenarioStore.GetAllAsync(cancellationToken);
    }
}
