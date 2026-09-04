using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Automation.Generation.Thetis;
using Microsoft.AspNetCore.Mvc;

namespace Automation.UI.Controllers;

public class PatientConfigurationsController(
    IPatientConfigurationStore store,
    IGenerationCatalogStore catalogStore,
    ITerminologyCodeLookup terminology) : Controller
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
    public IActionResult SeedFromProfile(string scenarioId, int resources = 50, bool? inpatient = null, bool? hypo = null)
    {
        var scenario = FhirGenerationCodes.GetScenarioById(scenarioId);
        if (scenario is null)
            return NotFound();

        var inpatientValue = inpatient ?? true;
        var hypoValue = hypo ?? ConfigurationQualification.ScenarioImpliesHypoglycemicInsulin(scenario);
        var spec = PatientSpecFactory.FromScenario(scenario, inpatientValue, hypoValue, Math.Max(1, resources));
        return Json(new
        {
            clinicalScenarioId = scenario.ScenarioId.ToString(),
            display = scenario.PrimaryDxDisplay,
            icd = scenario.PrimaryDxIcd,
            intent = PatientConfigurationTemplate.FromSpec(spec),
            exampleResourceCounts = PatientConfigurationTemplate.ExampleResourceCounts(spec)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Catalog(CancellationToken ct)
    {
        var catalogItems = await catalogStore.GetAllAsync(ct);
        if (catalogItems.Count == 0)
            catalogItems = GenerationCatalogSeed.FromHardcoded();

        return Json(new
        {
            scenarios = FhirGenerationCodes.ClinicalScenarios.Select(s => new
            {
                id = s.ScenarioId.ToString(),
                display = s.PrimaryDxDisplay,
                icd = s.PrimaryDxIcd,
                discharge = s.DischargeDispositionCode
            }),
            conditions = Project(catalogItems, GenerationCatalogKind.Condition),
            observations = Project(catalogItems, GenerationCatalogKind.Observation),
            procedures = Project(catalogItems, GenerationCatalogKind.Procedure),
            medications = Project(catalogItems, GenerationCatalogKind.Medication),
            serviceRequests = Project(catalogItems, GenerationCatalogKind.ServiceRequest),
            specimens = Project(catalogItems, GenerationCatalogKind.Specimen),
            encounterClasses = new[]
            {
                new { code = "IMP", display = "Inpatient" },
                new { code = "AMB", display = "Ambulatory" },
                new { code = "EMER", display = "Emergency" }
            },
            ipClassification = new
            {
                achClasses = EncounterIpClassification.NhsnInpatientClassCodes
                    .Concat(EncounterIpClassification.AchAdditionalClassCodes)
                    .ToArray(),
                hypoClasses = EncounterIpClassification.NhsnInpatientClassCodes.ToArray(),
                statuses = EncounterIpClassification.ValidIpStatuses.ToArray(),
                diabetesMedicationCodes = EncounterIpClassification.KnownDiabetesMedicationCodes(),
                hypoScenarioIds = FhirGenerationCodes.ClinicalScenarios
                    .Where(s => ConfigurationQualification.ScenarioImpliesHypoglycemicInsulin(s))
                    .Select(s => s.ScenarioId.ToString())
                    .ToArray()
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
            genders = new[] { "random", "female", "male", "other" },
            stayPatterns = ScheduledStayWindow.Catalog()
                .Select(p => new { value = p.Value, label = p.Label, hint = p.Hint, expectedInReport = p.ExpectedInReport })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LookupCode([FromBody] LookupCodeRequest request, CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Code))
            return BadRequest("Code is required.");
        if (!TryParseKind(request.Kind, out var kind))
            return BadRequest("Unknown catalog kind.");

        var item = await terminology.LookupAsync(kind, request.Code.Trim(), request.System, ct);
        if (item == null)
            return NotFound();
        if (request.Save)
            await catalogStore.MergeAsync([item], ct);
        return Json(ToPickerRow(item));
    }

    public sealed class LookupCodeRequest
    {
        public string? Kind { get; set; }
        public string? Code { get; set; }
        public string? System { get; set; }
        public bool Save { get; set; } = true;
    }

    private static bool TryParseKind(string? raw, out GenerationCatalogKind kind)
    {
        kind = default;
        var key = (raw ?? "").Trim();
        if (Enum.TryParse(key, ignoreCase: true, out kind))
            return true;
        kind = key.ToLowerInvariant() switch
        {
            "conditions" => GenerationCatalogKind.Condition,
            "observations" => GenerationCatalogKind.Observation,
            "procedures" => GenerationCatalogKind.Procedure,
            "medications" => GenerationCatalogKind.Medication,
            "servicerequests" => GenerationCatalogKind.ServiceRequest,
            "specimens" => GenerationCatalogKind.Specimen,
            _ => default
        };
        return key.ToLowerInvariant() is "conditions" or "observations" or "procedures"
            or "medications" or "servicerequests" or "specimens";
    }

    private static IEnumerable<object> Project(
        IReadOnlyList<GenerationCatalogItem> items,
        GenerationCatalogKind kind)
        => items.Where(i => i.Kind == kind)
            .GroupBy(i => i.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(i => i.Display ?? i.Code, StringComparer.OrdinalIgnoreCase)
            .Select(ToPickerRow);

    private static object ToPickerRow(GenerationCatalogItem item) => new
    {
        code = item.Code,
        display = string.IsNullOrWhiteSpace(item.Display) ? item.Code : item.Display,
        category = item.Category,
        unit = item.Unit,
        normLow = item.NormLow,
        normHigh = item.NormHigh,
        isLab = item.IsLab,
        incomplete = item.Incomplete,
        system = item.System
    };

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveInline([FromBody] PatientConfiguration model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
            return BadRequest("Name is required.");

        if (model.ClinicalScenarioIds is { Count: > 1 })
            model.ClinicalScenarioIds = [model.ClinicalScenarioIds[0]];
        if (model.ClinicalScenarioIds is not { Count: 1 }
            || FhirGenerationCodes.GetScenarioById(model.ClinicalScenarioIds[0]) is null)
        {
            return BadRequest("Select one Clinical Profile.");
        }

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
        model.ScheduledInpatientPattern ??= ScheduledStayWindow.DefaultPattern;
        StampDerivedQualification(model);

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
            ScheduledInpatientPattern = source.ScheduledInpatientPattern ?? ScheduledStayWindow.DefaultPattern,
            ClinicalScenarioIds = [.. source.ClinicalScenarioIds],
            ResourcesPerPatientMin = source.ResourcesPerPatientMin,
            ResourcesPerPatientMax = source.ResourcesPerPatientMax,
            Intent = PatientGenerationIntent.Clone(source.Intent) ?? new PatientGenerationIntent()
        };
        StampDerivedQualification(clone);
        await store.UpsertAsync(clone, ct);
        return Json(new { id = clone.Id });
    }

    internal static void StampDerivedQualification(PatientConfiguration model)
    {
        ConfigurationQualification.Stamp(
            model.Intent,
            model.ClinicalScenarioIds.FirstOrDefault(),
            out var eligibilities,
            out var cohortQualification);
        model.MeasureEligibilities = eligibilities;
        model.CohortQualification = cohortQualification;
    }
}
