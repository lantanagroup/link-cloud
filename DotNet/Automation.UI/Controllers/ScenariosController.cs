using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using Automation.UI.Services;
using Hl7.Fhir.Model;
using LantanaGroup.Automation;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Automation.UI.Controllers;

public class ScenariosController(
    IScenarioStore scenarioStore,
    IQueryPlanTemplateStore queryPlanTemplateStore,
    INormalizationStore normalizationStore,
    IOrganizationResourceMapTemplateStore organizationResourceMapTemplateStore,
    IOptions<AutomationConfig> automationConfig,
    IMongoDatabase database,
    IImportedBundleContentStore bundleContentStore,
    ILogger<ScenariosController> logger) : Controller
{
    private static readonly JsonSerializerOptions FhirJsonOptions = LantanaGroup.Link.Shared.Application.SerDes.LinkFhirSerializerOptions.ForFhirWithoutValidation();
    private const long LargeImportedBundleThresholdBytes = 5 * 1024 * 1024;
    private readonly IMongoCollection<ImportedBundleDocument> _bundles = database.GetCollection<ImportedBundleDocument>("automation_imported_bundles");

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var scenarios = await scenarioStore.GetAllAsync(ct);
        ViewBag.QueryPlanTemplates = await queryPlanTemplateStore.GetAllAsync(ct);
        ViewBag.NormalizationSuites = await normalizationStore.GetAllSuitesAsync(ct);
        ViewBag.OrganizationResourceMaps = await organizationResourceMapTemplateStore.GetAllAsync(ct);
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

        foreach (var cohort in model.PatientCohorts)
        {
            if (cohort.ResourcesPerPatientMin < 1)
                cohort.ResourcesPerPatientMin = 1;
            if (cohort.ResourcesPerPatientMax < cohort.ResourcesPerPatientMin)
                cohort.ResourcesPerPatientMax = cohort.ResourcesPerPatientMin;

            cohort.ScheduledInpatientPattern ??= ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod;

            var allNonQualifying = model.SelectedMeasures.Count > 0
                && model.SelectedMeasures.All(m => cohort.GetEligibility(m) == MeasureEligibility.NonQualifying);

            // Back-compat normalization for payloads that do not yet send cohortQualification.
            if (allNonQualifying)
                cohort.CohortQualification = MeasureEligibility.NonQualifying;
        }

        // ----- Imported-patient validation (fail save on bad input) -----
        var importValidation = await ValidateImportedPatientsAsync(model, ct);
        if (importValidation != null)
            return BadRequest(importValidation);

        model.NhsnOrganizationId = string.IsNullOrWhiteSpace(model.NhsnOrganizationId)
            ? GenerateRandomNhsnOrganizationId()
            : model.NhsnOrganizationId.Trim();
        model.UpdatedAt = DateTimeOffset.UtcNow;

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
    private async Task<string?> ValidateImportedPatientsAsync(TestScenarioDefinition model, CancellationToken ct)
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
            if (string.IsNullOrWhiteSpace(p.BundleJson) && !p.UploadedBundleId.HasValue)
                return $"Imported bundle '{p.FileName ?? p.PatientId}' is missing its uploaded reference.";

            string? bundleJson = p.BundleJson;
            if (string.IsNullOrWhiteSpace(bundleJson) && p.UploadedBundleId.HasValue)
            {
                var existing = await _bundles.Find(b => b.Id == p.UploadedBundleId.Value).FirstOrDefaultAsync(ct);
                if (existing == null)
                    return $"Imported bundle '{p.FileName ?? p.PatientId}' was not found. Please re-upload the file.";
                bundleJson = await bundleContentStore.ReadAsync(existing, ct);
                if (string.IsNullOrWhiteSpace(bundleJson))
                    return $"Imported bundle '{p.FileName ?? p.PatientId}' content is missing. Please re-upload the file.";
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
            if (p.UploadedBundleId.HasValue)
                p.BundleJson = null;
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
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> UploadImportedBundle([FromForm] UploadImportedBundleRequest request, CancellationToken ct)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest("Bundle file is required.");

        var sanitizedFileName = request.File.FileName.SanitizeAndRemove();

        logger.LogInformation("Received imported bundle upload '{FileName}' ({LengthBytes} bytes).", sanitizedFileName, request.File.Length);

        string json;
        using (var reader = new StreamReader(request.File.OpenReadStream(), Encoding.UTF8))
        {
            json = await reader.ReadToEndAsync(ct);
        }

        Bundle? bundle;
        try
        {
            bundle = JsonSerializer.Deserialize<Bundle>(
                json,
                FhirJsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse uploaded bundle '{FileName}' as FHIR JSON.", sanitizedFileName);
            return BadRequest($"Bundle '{request.File.FileName}' could not be parsed as FHIR: {ex.Message}");
        }

        if (bundle?.Entry == null || bundle.Entry.Count == 0)
            return BadRequest($"Bundle '{request.File.FileName}' contains no entries.");

        var patientResource = bundle.Entry
            .Select(e => e?.Resource)
            .OfType<Patient>()
            .FirstOrDefault();
        if (patientResource == null || string.IsNullOrWhiteSpace(patientResource.Id))
            return BadRequest($"Bundle '{request.File.FileName}' must contain a Patient resource with an id.");

        var effectiveBundle = bundle;
        var effectiveBundleJson = json;
        string? uploadWarning = null;
        var replaceAvailable = false;
        var canUseServerData = false;
        var patientExistsOnServer = false;
        var requiresReplaceForLargePatient = false;

        var existence = await TryCheckPatientExistsAsync(patientResource.Id, request.File.FileName, ct);
        patientExistsOnServer = existence.Exists == true;
        if (!string.IsNullOrWhiteSpace(existence.WarningMessage))
            uploadWarning = existence.WarningMessage;

        if (patientExistsOnServer)
        {
            replaceAvailable = true;
            requiresReplaceForLargePatient = request.File.Length >= LargeImportedBundleThresholdBytes;
            canUseServerData = !requiresReplaceForLargePatient;

            if (requiresReplaceForLargePatient)
            {
                uploadWarning =
                    $"Patient '{patientResource.Id}' already exists on the FHIR server and the uploaded bundle is large ({request.File.Length:N0} bytes). " +
                    "For large patients, use purge-and-replace to avoid partial server retrieval issues.";
            }
        }

        var organizationIds = (effectiveBundle.Entry ?? [])
            .Select(e => e?.Resource)
            .OfType<Organization>()
            .Select(o => o.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var now = DateTimeOffset.UtcNow;
        var hash = ComputeContentHash(effectiveBundleJson);

        var update = Builders<ImportedBundleDocument>.Update
            .SetOnInsert(b => b.Id, Guid.NewGuid())
            .SetOnInsert(b => b.ContentHash, hash)
            .SetOnInsert(b => b.ByteCount, 0)
            .SetOnInsert(b => b.CreatedAt, now)
            .SetOnInsert(b => b.IsLibraryEntry, false)
            .Set(b => b.PatientId, patientResource.Id)
            .Set(b => b.FileName, request.File.FileName)
            .Set(b => b.UpdatedAt, now);

        var doc = await _bundles.FindOneAndUpdateAsync(
            b => b.ContentHash == hash,
            update,
            new FindOneAndUpdateOptions<ImportedBundleDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            ct);

        var stored = await bundleContentStore.StoreAsync(doc.Id, doc.ContentHash, effectiveBundleJson, ct);
        await _bundles.UpdateOneAsync(
            b => b.Id == doc.Id,
            Builders<ImportedBundleDocument>.Update
                .Set(b => b.BundleBlobName, stored.BlobName)
                .Set(b => b.ByteCount, stored.ByteCount)
                .Set(b => b.BundleJson, null)
                .Set(b => b.UpdatedAt, now),
            cancellationToken: ct);

        logger.LogInformation(
            "Stored imported bundle '{FileName}' for patient '{PatientId}' in ABS blob '{BlobName}' ({ByteCount} bytes).",
            request.File.FileName,
            patientResource.Id,
            stored.BlobName,
            stored.ByteCount);

        return Json(new
        {
            bundleId = doc.Id,
            patientId = patientResource.Id,
            fileName = request.File.FileName,
            byteCount = stored.ByteCount,
            warningMessage = uploadWarning,
            replaceAvailable,
            patientExistsOnServer,
            canUseServerData,
            requiresReplaceForLargePatient,
            organizationId = organizationIds.Count == 1 ? organizationIds[0] : null,
            organizationIds
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplacePatientOnFhirServer([FromBody] ReplacePatientOnFhirServerRequest request, CancellationToken ct)
    {
        if (request.UploadedBundleId == Guid.Empty)
            return BadRequest("UploadedBundleId is required.");
        if (string.IsNullOrWhiteSpace(request.PatientId))
            return BadRequest("PatientId is required.");

        var doc = await _bundles.Find(b => b.Id == request.UploadedBundleId).FirstOrDefaultAsync(ct);
        if (doc == null)
            return NotFound("Uploaded bundle not found.");

        var bundleJson = await bundleContentStore.ReadAsync(doc, ct);
        if (string.IsNullOrWhiteSpace(bundleJson))
            return BadRequest("Uploaded bundle content is missing.");

        List<Bundle.EntryComponent> entries;
        try
        {
            entries = ImportedPatientLoader.ParseBundleEntries(bundleJson, request.PatientId.Trim());
        }
        catch (Exception ex)
        {
            return BadRequest($"Failed to parse uploaded bundle for replay: {ex.Message}");
        }

        var resourcesToDelete = entries
            .Select(e => e.Request?.Url)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(url => url!.StartsWith("Patient/", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .Cast<string>()
            .ToList();

        var cfg = automationConfig.Value;
        var loader = new FhirDataLoader(cfg.FhirServerBase, cfg.FhirServerOAuth, cfg.FhirServerBasicAuth);

        logger.LogInformation(
            "Replacing FHIR-server data for patient '{PatientId}' using uploaded bundle '{BundleId}'. Deleting {DeleteCount} resource path(s) first.",
            request.PatientId,
            request.UploadedBundleId,
            resourcesToDelete.Count);

        var purge = await loader.DeleteResourcesWithExpungeAsync(resourcesToDelete, ct);
        if (purge.Failed > 0)
        {
            logger.LogWarning(
                "FHIR purge before patient replace had failures for patient '{PatientId}': {Failed} failed, {Succeeded} succeeded. First errors: {Errors}",
                request.PatientId,
                purge.Failed,
                purge.Succeeded,
                string.Join(" | ", purge.Failures.Take(5)));
        }

        var replayBundles = BuildReplayBundles(entries, request.PatientId.Trim());
        var output = new RunAutomationOutput(message => logger.LogInformation("[FHIR Replay] {Message}", message));
        var replayOk = await loader.UploadBundlesSequentiallyAsync(output, replayBundles, $"[replace:{request.PatientId}] ");
        if (!replayOk)
        {
            return StatusCode(StatusCodes.Status502BadGateway,
                "Failed to replay uploaded bundle to FHIR server after purge. Uploaded bundle remains stored for scenario execution.");
        }

        var messageText = purge.Failed > 0
            ? $"Replaced FHIR-server data for patient '{request.PatientId}' with uploaded bundle, but purge had {purge.Failed} failed delete(s)."
            : $"Successfully replaced FHIR-server data for patient '{request.PatientId}' with uploaded bundle.";

        return Json(new
        {
            success = true,
            warningMessage = purge.Failed > 0 ? messageText : null,
            message = messageText,
            deletedSucceeded = purge.Succeeded,
            deletedFailed = purge.Failed,
            replayBundleCount = replayBundles.Count
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DiscardUploadedBundle([FromBody] DiscardUploadedBundleRequest request, CancellationToken ct)
    {
        if (request.UploadedBundleId == Guid.Empty)
            return BadRequest("UploadedBundleId is required.");

        var doc = await _bundles.Find(b => b.Id == request.UploadedBundleId).FirstOrDefaultAsync(ct);
        if (doc == null)
            return Json(new { success = true, deleted = false, message = "Bundle not found; nothing to discard." });

        if (doc.IsLibraryEntry || (doc.ScenarioIds?.Count ?? 0) > 0)
        {
            return Json(new
            {
                success = true,
                deleted = false,
                message = "Bundle was retained because it is already referenced."
            });
        }

        await bundleContentStore.DeleteAsync(doc, ct);
        await _bundles.DeleteOneAsync(b => b.Id == doc.Id, ct);

        logger.LogInformation("Discarded uploaded bundle '{BundleId}' (unreferenced).", doc.Id);
        return Json(new { success = true, deleted = true });
    }

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
                if (!string.IsNullOrWhiteSpace(request.BundleJson))
                {
                    bundleJson = request.BundleJson;
                }
                else if (request.UploadedBundleId.HasValue)
                {
                    var existing = await _bundles.Find(b => b.Id == request.UploadedBundleId.Value).FirstOrDefaultAsync(ct);
                    if (existing == null)
                        return BadRequest("Uploaded bundle was not found. Please re-upload.");
                    bundleJson = await bundleContentStore.ReadAsync(existing, ct) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(bundleJson))
                        return BadRequest("Uploaded bundle content is missing. Please re-upload.");
                }
                else
                {
                    return BadRequest("BundleJson or UploadedBundleId is required for Bundle source.");
                }
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

        var organizationIds = entries
            .Select(e => e.Resource)
            .OfType<Organization>()
            .Select(o => o.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return Json(new
        {
            patientId = resolvedPatientId,
            measureEligibilities = classification.MeasureEligibilities
                .ToDictionary(kv => kv.Key.ToString(), kv => kv.Value.ToString()),
            detectedClinicalScenarioId = classification.DetectedClinicalScenarioId,
            encounterStart = encStart?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            encounterEnd = encEnd?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            organizationId = organizationIds.Count == 1 ? organizationIds[0] : null,
            organizationIds
        });
    }

    public sealed class ClassifyImportedRequest
    {
        public ImportedPatientSource Source { get; set; } = ImportedPatientSource.ExistingId;
        public string? PatientId { get; set; }
        public string? BundleJson { get; set; }
        public Guid? UploadedBundleId { get; set; }
        public List<ProfiledMeasureType> Measures { get; set; } = [];
    }

    public sealed class UploadImportedBundleRequest
    {
        public IFormFile? File { get; set; }
    }

    public sealed class ReplacePatientOnFhirServerRequest
    {
        public Guid UploadedBundleId { get; set; }
        public string PatientId { get; set; } = string.Empty;
    }

    public sealed class DiscardUploadedBundleRequest
    {
        public Guid UploadedBundleId { get; set; }
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
            NhsnOrganizationId = source.NhsnOrganizationId,
            PatientCohorts = source.PatientCohorts
                .Select(c => new PatientCohortDefinition
                {
                    PatientCount = c.PatientCount,
                    CohortQualification = c.CohortQualification,
                    MeasureEligibilities = new(c.MeasureEligibilities),
                    EligibleClinicalScenarioIds = [.. c.EligibleClinicalScenarioIds],
                    ResourcesPerPatientMin = c.ResourcesPerPatientMin,
                    ResourcesPerPatientMax = c.ResourcesPerPatientMax,
                    ScheduledInpatientPattern = c.ScheduledInpatientPattern
                })
                .ToList(),
            QueryPlanTemplateId = source.QueryPlanTemplateId,
            NormalizationSuiteId = source.NormalizationSuiteId,
            OrganizationResourceMapTemplateId = source.OrganizationResourceMapTemplateId,
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
                    UploadedBundleId = p.UploadedBundleId,
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
                    UploadedBundleId = p.UploadedBundleId,
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

    private static string ComputeContentHash(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string GenerateRandomNhsnOrganizationId()
    {
        return Random.Shared.Next(10000, 100000).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<(bool? Exists, string? WarningMessage)> TryCheckPatientExistsAsync(
        string patientId,
        string fileName,
        CancellationToken ct)
    {
        var cfg = automationConfig.Value;
        var loader = new FhirDataLoader(cfg.FhirServerBase, cfg.FhirServerOAuth, cfg.FhirServerBasicAuth);

        try
        {
            var exists = await loader.PatientExistsAsync(patientId, ct);
            return (exists, null);
        }
        catch (Exception ex)
        {
            var sanitizedFileName = fileName.SanitizeAndRemove();
            logger.LogWarning(
                ex,
                "Failed checking if uploaded patient '{PatientId}' from file '{FileName}' exists on FHIR server.",
                patientId,
                sanitizedFileName);

            var warning =
                $"Automation UI could not check whether patient '{patientId}' exists on the FHIR server. " +
                "The uploaded bundle will be used as provided.";

            return (null, warning);
        }
    }

    private static IReadOnlyList<(string Name, string Json)> BuildReplayBundles(
        IReadOnlyList<Bundle.EntryComponent> entries,
        string patientId)
    {
        const int maxEntriesPerBundle = 500;
        var bundles = new List<(string Name, string Json)>();
        var chunkIndex = 0;

        for (var offset = 0; offset < entries.Count; offset += maxEntriesPerBundle)
        {
            var chunk = entries.Skip(offset).Take(maxEntriesPerBundle).ToList();
            var replay = new Bundle
            {
                Type = Bundle.BundleType.Batch,
                Entry = chunk
            };

            var json = JsonSerializer.Serialize(replay, FhirJsonOptions);
            bundles.Add(($"{patientId}_replace_chunk{++chunkIndex:00}", json));
        }

        return bundles;
    }

}
