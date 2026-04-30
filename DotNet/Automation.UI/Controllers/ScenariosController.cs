using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using Hl7.Fhir.Model;
using LantanaGroup.Automation;
using LantanaGroup.Link.Automation.Link.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Automation.UI.Controllers;

public class ScenariosController(
    IScenarioStore scenarioStore,
    IQueryPlanTemplateStore queryPlanTemplateStore,
    IOptions<AutomationConfig> automationConfig) : Controller
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
            return StatusCode(StatusCodes.Status403Forbidden, "Forbidden: system scenario cannot be modified.");

        model.IsSystemScenario = false;
        model.UpdatedAt = DateTimeOffset.UtcNow;

        if (model.ResourcesPerPatientMax < model.ResourcesPerPatientMin)
            model.ResourcesPerPatientMax = model.ResourcesPerPatientMin;

        // ----- Imported-patient validation (fail save on bad input) -----
        var importValidation = ValidateImportedPatients(model);
        if (importValidation != null)
            return BadRequest(importValidation);

        await scenarioStore.UpsertAsync(model, ct);
        return Json(new { id = model.Id });
    }

    /// <summary>
    /// Validates the imported-patient lists on a scenario being saved. Returns a user-facing
    /// error string when validation fails, or null when the lists are acceptable.
    /// Bundle JSON must parse and contain a Patient resource whose id matches the configured
    /// PatientId; ID-based imports must have a non-empty PatientId.
    ///
    /// Note: imported patient encounter dates are NOT required to sit inside the configured
    /// Report Period. A scenario with date mismatches is a legitimate test case (proper
    /// disqualification by measure-eval). The UI surfaces the mismatch as a warning so the
    /// user can design the scenario knowingly.
    /// </summary>
    private static string? ValidateImportedPatients(TestScenarioDefinition model)
    {
        // Validate the period itself.
        if (model.ReportPeriodStart.HasValue && model.ReportPeriodEnd.HasValue
            && model.ReportPeriodEnd.Value < model.ReportPeriodStart.Value)
            return "Report Period end must be on or after Report Period start.";

        foreach (var p in model.ImportedPatientIds ?? [])
        {
            if (string.IsNullOrWhiteSpace(p.PatientId))
                return "Each imported patient (by ID) must have a non-empty PatientId.";
            p.Source = ImportedPatientSource.ExistingId;
            p.BundleJson = null;
        }

        foreach (var p in model.ImportedPatientBundles ?? [])
        {
            if (string.IsNullOrWhiteSpace(p.BundleJson))
                return $"Imported bundle '{p.FileName ?? p.PatientId}' is missing its bundle JSON.";

            Bundle? bundle;
            try
            {
                bundle = System.Text.Json.JsonSerializer.Deserialize<Bundle>(
                    p.BundleJson,
                    LantanaGroup.Link.Shared.Application.SerDes.LinkFhirSerializerOptions.ForFhirWithoutValidation());
            }
            catch (Exception ex)
            {
                return $"Imported bundle '{p.FileName ?? p.PatientId}' could not be parsed as FHIR: {ex.Message}";
            }

            if (bundle?.Entry == null || bundle.Entry.Count == 0)
                return $"Imported bundle '{p.FileName ?? p.PatientId}' contains no entries.";

            var patientResource = bundle.Entry
                .Select(e => e?.Resource)
                .OfType<Patient>()
                .FirstOrDefault();

            if (patientResource == null || string.IsNullOrWhiteSpace(patientResource.Id))
                return $"Imported bundle '{p.FileName ?? "(no name)"}' must contain a Patient resource with an id.";

            if (string.IsNullOrWhiteSpace(p.PatientId))
            {
                p.PatientId = patientResource.Id;
            }
            else if (!string.Equals(p.PatientId, patientResource.Id, StringComparison.Ordinal))
            {
                return $"Imported bundle '{p.FileName ?? p.PatientId}' Patient.id '{patientResource.Id}' does not match configured PatientId '{p.PatientId}'.";
            }

            p.Source = ImportedPatientSource.Bundle;
        }

        return null;
    }

    private static DateTime? ParseFhirDateTime(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var dto))
            return dto.UtcDateTime;
        return null;
    }

    /// <summary>
    /// Classifies an imported patient's FHIR resources to seed per-measure Q/NQ checkboxes
    /// in the scenario editor UI. For <c>ExistingId</c> source the patient is fetched from
    /// the configured FHIR server via <c>Patient/{id}/$everything</c>; for <c>Bundle</c>
    /// source the supplied JSON is parsed directly.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClassifyImported([FromBody] ClassifyImportedRequest request, CancellationToken ct)
    {
        if (request == null)
            return BadRequest("Request body is required.");
        if (request.Measures == null || request.Measures.Count == 0)
            return BadRequest("At least one measure is required for classification.");

        string bundleJson;
        try
        {
            if (request.Source == ImportedPatientSource.ExistingId)
            {
                if (string.IsNullOrWhiteSpace(request.PatientId))
                    return BadRequest("PatientId is required for ExistingId source.");

                var cfg = automationConfig.Value;
                var loader = new FhirDataLoader(cfg.FhirServerBase, cfg.FhirServerOAuth, cfg.FhirServerBasicAuth);
                bundleJson = await loader.FetchPatientEverythingAsync(request.PatientId.Trim(), ct);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.BundleJson))
                    return BadRequest("BundleJson is required for Bundle source.");
                bundleJson = request.BundleJson!;
            }
        }
        catch (Exception ex)
        {
            return BadRequest($"Failed to load patient data: {ex.Message}");
        }

        Bundle? bundle;
        try
        {
            bundle = System.Text.Json.JsonSerializer.Deserialize<Bundle>(
                bundleJson,
                LantanaGroup.Link.Shared.Application.SerDes.LinkFhirSerializerOptions.ForFhirWithoutValidation());
        }
        catch (Exception ex)
        {
            return BadRequest($"Failed to parse FHIR bundle: {ex.Message}");
        }

        if (bundle?.Entry == null || bundle.Entry.Count == 0)
            return BadRequest("FHIR bundle contains no entries.");

        var entries = bundle.Entry.Where(e => e?.Resource != null).ToList();

        var patientResource = entries
            .Select(e => e.Resource)
            .OfType<Patient>()
            .FirstOrDefault();

        var resolvedPatientId = patientResource?.Id ?? request.PatientId;

        var classification = ImportedPatientClassifier.Classify(entries, request.Measures);

        // Compute encounter date range so the UI can auto-suggest a Report Period.
        DateTime? encStart = null, encEnd = null;
        foreach (var enc in entries.Select(e => e.Resource).OfType<Encounter>())
        {
            if (enc.Period == null) continue;
            var s = ParseFhirDateTime(enc.Period.Start);
            var e = ParseFhirDateTime(enc.Period.End) ?? s;
            if (s.HasValue && (encStart == null || s.Value < encStart.Value)) encStart = s;
            if (e.HasValue && (encEnd == null || e.Value > encEnd.Value)) encEnd = e;
        }

        return Json(new
        {
            patientId = resolvedPatientId,
            measureEligibilities = classification.MeasureEligibilities
                .ToDictionary(kv => kv.Key.ToString(), kv => kv.Value.ToString()),
            detectedClinicalScenarioId = classification.DetectedClinicalScenarioId,
            encounterStart = encStart?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            encounterEnd = encEnd?.ToString("yyyy-MM-ddTHH:mm:ssZ")
        });
    }

    public sealed class ClassifyImportedRequest
    {
        public ImportedPatientSource Source { get; set; } = ImportedPatientSource.ExistingId;
        public string? PatientId { get; set; }
        public string? BundleJson { get; set; }
        public List<ProfiledMeasureType> Measures { get; set; } = [];
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteInline([FromBody] IdRequest request, CancellationToken ct)
    {
        var scenario = await scenarioStore.GetByIdAsync(request.Id, ct);
        if (scenario == null) return NotFound();
        if (scenario.IsSystemScenario) return StatusCode(StatusCodes.Status403Forbidden, "Forbidden: system scenario cannot be deleted.");

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
            QueryPlanTemplateId = source.QueryPlanTemplateId,
            CleanupServiceData = source.CleanupServiceData,
            CleanupFhirData = source.CleanupFhirData,
            ReportPeriodStart = source.ReportPeriodStart,
            ReportPeriodEnd = source.ReportPeriodEnd,
            ImportedPatientIds = source.ImportedPatientIds
                .Select(p => new ImportedPatientInput
                {
                    Source = p.Source,
                    PatientId = p.PatientId,
                    FileName = p.FileName,
                    BundleJson = p.BundleJson,
                    AutoDetect = p.AutoDetect,
                    MeasureEligibilities = new(p.MeasureEligibilities),
                    DetectedClinicalScenarioId = p.DetectedClinicalScenarioId
                })
                .ToList(),
            ImportedPatientBundles = source.ImportedPatientBundles
                .Select(p => new ImportedPatientInput
                {
                    Source = p.Source,
                    PatientId = p.PatientId,
                    FileName = p.FileName,
                    BundleJson = p.BundleJson,
                    AutoDetect = p.AutoDetect,
                    MeasureEligibilities = new(p.MeasureEligibilities),
                    DetectedClinicalScenarioId = p.DetectedClinicalScenarioId
                })
                .ToList(),
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
