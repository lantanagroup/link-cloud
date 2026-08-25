using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using LantanaGroup.Automation.Generation;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class PatientConfigurationsController(IPatientConfigurationStore store) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var items = await store.GetAllAsync(ct);
        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> GetJson(Guid id, CancellationToken ct)
    {
        var item = await store.GetByIdAsync(id, ct);
        if (item == null)
            return NotFound();
        return Json(item);
    }

    [HttpGet]
    public IActionResult Catalog()
    {
        return Json(new
        {
            scenarios = FhirGenerationCodes.ClinicalScenarios.Select(s => new
            {
                id = s.ScenarioId.ToString(),
                display = s.PrimaryDxDisplay,
                icd = s.PrimaryDxIcd,
                discharge = s.DischargeDispositionCode
            }),
            conditions = FhirGenerationCodes.Conditions.Select(c => new
            {
                code = c.Code,
                display = c.Display,
                category = c.Category
            }),
            observations = FhirGenerationCodes.Observations.Select(o => new
            {
                code = o.Code,
                display = o.Display,
                category = o.Category,
                unit = o.Unit,
                normLow = o.NormLow,
                normHigh = o.NormHigh
            }),
            procedures = FhirGenerationCodes.Procedures.Select(p => new
            {
                code = p.Code,
                display = p.Display
            }),
            medications = FhirGenerationCodes.Medications.Select(m => new
            {
                code = m.RxCode,
                display = m.Display
            }),
            serviceRequests = FhirGenerationCodes.ServiceRequests.Select(s => new
            {
                code = s.Code,
                display = s.Display,
                isLab = s.IsLab
            }),
            specimens = FhirGenerationCodes.Specimens.Select(s => new
            {
                code = s.TypeCode,
                display = s.TypeDisplay
            }),
            encounterClasses = new[]
            {
                new { code = "IMP", display = "Inpatient" },
                new { code = "AMB", display = "Ambulatory" },
                new { code = "EMER", display = "Emergency" }
            },
            encounterStatuses = new[] { "finished", "in-progress" },
            dischargeDispositions = new[]
            {
                new { code = "home", display = "Home" },
                new { code = "snf", display = "Skilled nursing facility" },
                new { code = "rehab", display = "Inpatient rehabilitation" },
                new { code = "exp", display = "Expired" },
                new { code = "oth", display = "Other" }
            },
            genders = new[] { "random", "female", "male", "other" }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveInline([FromBody] PatientConfiguration model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            return BadRequest("Name is required.");

        var existing = await store.GetByIdAsync(model.Id, ct);
        if (existing is { IsSystem: true })
            return StatusCode(StatusCodes.Status403Forbidden, "Forbidden: system configuration cannot be modified.");

        model.IsSystem = false;
        model.UpdatedAt = DateTimeOffset.UtcNow;
        model.Intent ??= new PatientGenerationIntent();
        if (model.ResourcesPerPatientMin < 1)
            model.ResourcesPerPatientMin = 1;
        if (model.ResourcesPerPatientMax < model.ResourcesPerPatientMin)
            model.ResourcesPerPatientMax = model.ResourcesPerPatientMin;

        await store.UpsertAsync(model, ct);
        return Json(new { id = model.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInline([FromBody] IdRequest request, CancellationToken ct)
    {
        if (!this.TryValidateIdRequest(request, out var badRequest))
            return badRequest;

        var item = await store.GetByIdAsync(request.Id, ct);
        if (item == null)
            return NotFound();
        if (item.IsSystem)
            return StatusCode(StatusCodes.Status403Forbidden, "Forbidden: system configuration cannot be deleted.");

        await store.DeleteAsync(request.Id, ct);
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloneInline([FromBody] IdRequest request, CancellationToken ct)
    {
        if (!this.TryValidateIdRequest(request, out var badRequest))
            return badRequest;

        var source = await store.GetByIdAsync(request.Id, ct);
        if (source == null)
            return NotFound();

        var clone = new PatientConfiguration
        {
            Id = Guid.NewGuid(),
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            IsSystem = false,
            UpdatedAt = DateTimeOffset.UtcNow,
            CohortQualification = source.CohortQualification,
            MeasureEligibilities = new(source.MeasureEligibilities),
            ScheduledInpatientPattern = source.ScheduledInpatientPattern,
            ClinicalScenarioIds = [.. source.ClinicalScenarioIds],
            ResourcesPerPatientMin = source.ResourcesPerPatientMin,
            ResourcesPerPatientMax = source.ResourcesPerPatientMax,
            Intent = PatientGenerationIntent.Clone(source.Intent) ?? new PatientGenerationIntent()
        };
        await store.UpsertAsync(clone, ct);
        return Json(new { id = clone.Id });
    }
}
