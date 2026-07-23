using LantanaGroup.Link.Automation.Link.Helpers;
using System.Text.Json;

namespace LantanaGroup.Link.Automation.Link.Validation;

/// <summary>
/// Validates the report manifest and patient artifacts stored in internal ABS
/// (downloaded through Submission API with external=false) against expected test inputs
/// and persisted pipeline state.
/// </summary>
public class ReportAbsManifestValidator
{
    private const int MaxErrors = 200;
    private const string ApplicablePeriodExtensionUrl = "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/StructureDefinition/link-patient-list-applicable-period-extension";

    private static readonly HashSet<string> DerivedResourceTypes =
    [
        "MeasureReport",
        "OperationOutcome"
    ];

    private readonly IAutomationOutput _output;
    private readonly PipelineDataReader _reader;

    public ReportAbsManifestValidator(IAutomationOutput output, PipelineDataReader reader)
    {
        _output = output;
        _reader = reader;
    }

    /// <summary>
    /// Backward-compatible overload that accepts a single expected measure ID.
    /// </summary>
    public Task ValidateAllAsync(
        IDictionary<string, object> internalAbsResources,
        IReadOnlyCollection<string> expectedPatientIds,
        string expectedMeasureId,
        string expectedStartDate,
        string expectedEndDate,
        string? facilityId = null,
        string? reportId = null,
        IReadOnlyList<(string Name, string Json)>? generatedBundles = null,
        IReadOnlyCollection<string>? expectedManifestPatientListIds = null,
        bool expectDataAcquisitionData = true,
        GenerationManifest? manifest = null)
    {
        return ValidateAllAsync(
            internalAbsResources,
            expectedPatientIds,
            (IReadOnlyList<string>)[expectedMeasureId],
            expectedStartDate,
            expectedEndDate,
            facilityId,
            reportId,
            generatedBundles,
            expectedManifestPatientListIds,
            expectDataAcquisitionData,
            manifest);
    }

    public async Task ValidateAllAsync(
        IDictionary<string, object> internalAbsResources,
        IReadOnlyCollection<string> expectedPatientIds,
        IReadOnlyList<string> expectedMeasureIds,
        string expectedStartDate,
        string expectedEndDate,
        string? facilityId = null,
        string? reportId = null,
        IReadOnlyList<(string Name, string Json)>? generatedBundles = null,
        IReadOnlyCollection<string>? expectedManifestPatientListIds = null,
        bool expectDataAcquisitionData = true,
        GenerationManifest? manifest = null)
    {
        var errors = new List<string>();

        var expectedSubmittedPatientIds = expectedPatientIds;
        var expectedListPatientIds = expectedManifestPatientListIds ?? expectedSubmittedPatientIds;

        if (!internalAbsResources.TryGetValue("manifest.ndjson", out var manifestObj) || manifestObj is not string manifestNdjson)
        {
            AddError(errors, "manifest.ndjson was not found in internal ABS artifacts.");
            await FailIfNeededAsync(errors);
            return;
        }

        var expectedPatientFiles = expectedSubmittedPatientIds
            .Select(id => $"patient-{id}.ndjson")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var actualPatientFiles = internalAbsResources.Keys
            .Where(k => k.StartsWith("patient-", StringComparison.OrdinalIgnoreCase) && k.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var expected in expectedPatientFiles)
        {
            if (!actualPatientFiles.Contains(expected))
                AddError(errors, $"Missing expected patient artifact in internal ABS: {expected}");
        }

        var unexpected = actualPatientFiles.Except(expectedPatientFiles, StringComparer.OrdinalIgnoreCase).Take(10).ToList();
        foreach (var extra in unexpected)
            AddError(errors, $"Unexpected patient artifact in internal ABS: {extra}");

        var manifestResources = ParseNdjson(manifestNdjson, "manifest.ndjson", errors);
        ValidateManifest(manifestResources, expectedListPatientIds, expectedMeasureIds, expectedStartDate, expectedEndDate, errors);

        var parsedPatientResources = new List<AbsResourceRecord>();
        var patientMeasureReportIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var patientId in expectedSubmittedPatientIds)
        {
            var fileName = $"patient-{patientId}.ndjson";
            if (!internalAbsResources.TryGetValue(fileName, out var patientObj) || patientObj is not string patientNdjson)
                continue;

            var patientResources = ParseNdjson(patientNdjson, fileName, errors);
            parsedPatientResources.AddRange(patientResources
                .Select(r => new AbsResourceRecord(fileName, patientId, GetString(r, "resourceType") ?? string.Empty, GetString(r, "id") ?? string.Empty, r)));

            ValidatePatientArtifact(patientId, patientResources, patientMeasureReportIds, errors);
        }

        HashSet<string>? expectedSubmittedMeasureReportIds = null;
        if (!string.IsNullOrWhiteSpace(reportId) && Guid.TryParse(reportId, out var scheduleIdForMeasureReports))
        {
            try
            {
                var expectedSubmittedPatientSet = expectedSubmittedPatientIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToHashSet(StringComparer.Ordinal);

                var entries = await _reader.GetReportEntriesWithMeasureReportsAsync(scheduleIdForMeasureReports);
                expectedSubmittedMeasureReportIds = entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.PatientId)
                                && expectedSubmittedPatientSet.Contains(e.PatientId)
                                && string.Equals(e.SubmissionStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
                    .SelectMany(e => e.MeasureReports)
                    .Select(mr => mr.MeasureReportId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToHashSet(StringComparer.Ordinal);

                if (manifest != null)
                {
                    foreach (var entry in entries)
                    {
                        if (string.IsNullOrWhiteSpace(entry.PatientId)
                            || !expectedSubmittedPatientSet.Contains(entry.PatientId)
                            || !string.Equals(entry.SubmissionStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var reportable = entry.MeasureReports
                            .Where(mr => IsReadyForValidation(mr.Status))
                            .ToList();

                        var actualReportableCount = reportable.Count;
                        var predictedCounts = manifest.GetExpectedAbsCountsForPatient(entry.PatientId);
                        var predictedMeasureReportCount = 0;
                        if (predictedCounts != null)
                            predictedCounts.TryGetValue("MeasureReport", out predictedMeasureReportCount);

                        if (predictedMeasureReportCount != actualReportableCount)
                        {
                            AddError(
                                errors,
                                $"ABS patient={entry.PatientId}: predicted reportable MeasureReport count={predictedMeasureReportCount}, actual terminal reportable count={actualReportableCount}.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[ABS][WARN] Could not read submitted MeasureReport ids for manifest reference reconciliation: {ex.Message}");
            }
        }

        ValidateManifestMeasureReportReferences(manifestResources, patientMeasureReportIds, expectedSubmittedMeasureReportIds, errors);

        if (!string.IsNullOrWhiteSpace(facilityId) && !string.IsNullOrWhiteSpace(reportId))
        {
            if (generatedBundles is { Count: > 0 })
            {
                ValidateGeneratedBundleReconciliation(generatedBundles, parsedPatientResources, errors);
            }
            else if (manifest != null && manifest.ResourceKeysByPatient.Count > 0)
            {
                ValidateGeneratedManifestReconciliation(manifest, parsedPatientResources, errors);
            }
        }

        // When a manifest is available, validate ABS resource counts against the concrete
        // generated input - no DB interrogation or baseline needed.
        if (manifest != null)
        {
            // Populate the count-level expectation for OperationOutcome from the authoritative
            // source: Report.ReportEntry.ReportingStatus. ValidationCompleteListener appends
            // exactly one OperationOutcome to a patient's aggregate blob when its
            // ValidationComplete.IsValid == false (status becomes FailedValidation).
            if (!string.IsNullOrWhiteSpace(reportId) && Guid.TryParse(reportId, out var scheduleId))
            {
                await PopulateExpectedOperationOutcomesFromReportEntriesAsync(manifest, scheduleId);
            }

            ValidateAbsResourceCountsAgainstManifest(
                manifest,
                parsedPatientResources,
                expectedSubmittedPatientIds,
                errors);
        }

        await FailIfNeededAsync(errors);
    }

    /// <summary>
    /// Populates <see cref="GenerationManifest.ExpectedOperationOutcomeCountByPatient"/>
    /// from Report DB entries. Every patient whose <c>ReportingStatus</c> is
    /// <c>FailedValidation</c> gets exactly one OperationOutcome appended to their ABS
    /// patient file by <c>ValidationCompleteListener.ProcessMessageAsync</c>.
    /// </summary>
    private async Task PopulateExpectedOperationOutcomesFromReportEntriesAsync(GenerationManifest manifest, Guid scheduleId)
    {
        try
        {
            var entries = await _reader.GetReportEntriesWithMeasureReportsAsync(scheduleId);
            var expected = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.PatientId))
                    continue;

                if (string.Equals(entry.ReportingStatus, "FailedValidation", StringComparison.OrdinalIgnoreCase))
                    expected[entry.PatientId] = 1;
            }

            manifest.ExpectedOperationOutcomeCountByPatient = expected;

            if (expected.Count > 0)
            {
                _output.WriteLine($"[ABS] Predicting 1 OperationOutcome for {expected.Count} patient(s) with FailedValidation status.");
            }
        }
        catch (Exception ex)
        {
            // Do not fail the validator on a DB read hiccup - just log. The strict check
            // will then flag any unpredicted OperationOutcomes, which is the safe default.
            _output.WriteLine($"[ABS][WARN] Could not read ReportEntry statuses to predict OperationOutcomes: {ex.Message}");
        }
    }

    private void ValidateManifest(
        List<JsonElement> manifestResources,
        IReadOnlyCollection<string> expectedPatientIds,
        IReadOnlyList<string> expectedMeasureIds,
        string expectedStartDate,
        string expectedEndDate,
        List<string> errors)
    {
        if (manifestResources.Count == 0)
        {
            AddError(errors, "manifest.ndjson is empty.");
            return;
        }

        var duplicateIds = manifestResources
            .Select(r => (Type: GetString(r, "resourceType"), Id: GetString(r, "id")))
            .Where(t => !string.IsNullOrWhiteSpace(t.Type) && !string.IsNullOrWhiteSpace(t.Id))
            .GroupBy(t => $"{t.Type}/{t.Id}")
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .Take(10)
            .ToList();

        foreach (var dup in duplicateIds)
            AddError(errors, $"Duplicate manifest resource id detected: {dup}");

        var orgResources = manifestResources.Where(r => IsType(r, "Organization")).ToList();
        var deviceResources = manifestResources.Where(r => IsType(r, "Device")).ToList();
        var listResources = manifestResources.Where(r => IsType(r, "List")).ToList();

        if (orgResources.Count != 1) AddError(errors, $"Manifest should contain exactly one Organization resource. Actual={orgResources.Count}");
        if (deviceResources.Count != 1) AddError(errors, $"Manifest should contain exactly one Device resource. Actual={deviceResources.Count}");
        if (listResources.Count != 1) AddError(errors, $"Manifest should contain exactly one List resource. Actual={listResources.Count}");

        var patientList = listResources.FirstOrDefault();
        if (patientList.ValueKind == JsonValueKind.Undefined)
        {
            AddError(errors, "Manifest does not contain a patient List resource.");
        }
        else
        {
            ValidateManifestPatientList(patientList, expectedPatientIds, expectedStartDate, expectedEndDate, errors);
        }

        var subjectListReports = manifestResources
            .Where(r => IsType(r, "MeasureReport") && string.Equals(GetString(r, "type"), "subject-list", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (subjectListReports.Count == 0)
            AddError(errors, "Manifest does not contain any subject-list MeasureReport resources.");

        // Track which expected measure IDs are covered by subject-list reports.
        var coveredMeasureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mr in subjectListReports)
        {
            var measure = GetString(mr, "measure");
            var matchedMeasure = expectedMeasureIds.FirstOrDefault(id =>
                !string.IsNullOrWhiteSpace(measure) && measure.Contains(id, StringComparison.OrdinalIgnoreCase));

            if (matchedMeasure != null)
            {
                coveredMeasureIds.Add(matchedMeasure);
            }
            else
            {
                AddError(errors, $"Manifest MeasureReport measure does not match any expected measure id [{string.Join(", ", expectedMeasureIds)}]. Actual='{measure ?? "(null)"}'");
            }

            var reporterRef = GetNestedString(mr, "reporter", "reference");
            if (string.IsNullOrWhiteSpace(reporterRef) || !reporterRef.StartsWith("Organization/", StringComparison.Ordinal))
                AddError(errors, "Manifest MeasureReport reporter reference is missing or invalid.");

            var periodStart = GetNestedString(mr, "period", "start");
            var periodEnd = GetNestedString(mr, "period", "end");

            if (!AreSameInstant(periodStart, expectedStartDate))
                AddError(errors, $"Manifest MeasureReport period.start mismatch. Expected '{expectedStartDate}', actual '{periodStart ?? "(null)"}'");

            if (!AreSameInstant(periodEnd, expectedEndDate))
                AddError(errors, $"Manifest MeasureReport period.end mismatch. Expected '{expectedEndDate}', actual '{periodEnd ?? "(null)"}'");

            var subjectReference = GetNestedString(mr, "subject", "reference");
            if (!string.IsNullOrWhiteSpace(subjectReference) &&
                !subjectReference.StartsWith("Patient/", StringComparison.Ordinal))
            {
                AddError(errors, "Manifest MeasureReport subject reference is present but invalid.");
            }
        }

        // Verify all expected measures are represented in the manifest.
        foreach (var expectedId in expectedMeasureIds)
        {
            if (!coveredMeasureIds.Contains(expectedId))
                AddError(errors, $"Manifest is missing a subject-list MeasureReport for expected measure '{expectedId}'.");
        }
    }

    private static bool AreSameInstant(string? actual, string? expected)
    {
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected))
            return string.Equals(actual, expected, StringComparison.Ordinal);

        if (DateTimeOffset.TryParse(actual, out var actualDto) && DateTimeOffset.TryParse(expected, out var expectedDto))
        {
            // Allow up to 1 second of tolerance to account for sub-second precision
            // differences across serialization layers (DB milliseconds vs FHIR microseconds).
            var diff = (actualDto.ToUniversalTime() - expectedDto.ToUniversalTime()).Duration();
            return diff < TimeSpan.FromSeconds(1);
        }

        return string.Equals(actual, expected, StringComparison.Ordinal);
    }

    private void ValidatePatientArtifact(
        string patientId,
        List<JsonElement> resources,
        HashSet<string> patientMeasureReportIds,
        List<string> errors)
    {
        if (resources.Count == 0)
        {
            AddError(errors, $"patient-{patientId}.ndjson is empty.");
            return;
        }

        var patientResourceFound = resources.Any(r => IsType(r, "Patient") && string.Equals(GetString(r, "id"), patientId, StringComparison.Ordinal));
        var patientReferenceFound = resources.Any(r =>
            string.Equals(GetNestedString(r, "subject", "reference"), $"Patient/{patientId}", StringComparison.Ordinal) ||
            string.Equals(GetNestedString(r, "patient", "reference"), $"Patient/{patientId}", StringComparison.Ordinal) ||
            string.Equals(GetNestedString(r, "beneficiary", "reference"), $"Patient/{patientId}", StringComparison.Ordinal));

        if (!patientResourceFound && !patientReferenceFound)
            AddError(errors, $"patient-{patientId}.ndjson does not contain Patient/{patientId} or a matching patient reference.");

        var duplicates = resources
            .Select(r => (Type: GetString(r, "resourceType"), Id: GetString(r, "id")))
            .Where(x => !string.IsNullOrWhiteSpace(x.Type) && !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => $"{x.Type}/{x.Id}")
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .Take(10)
            .ToList();

        foreach (var dup in duplicates)
            AddError(errors, $"patient-{patientId}.ndjson contains duplicate resource {dup}.");

        foreach (var resource in resources)
        {
            var type = GetString(resource, "resourceType") ?? "(unknown)";
            var id = GetString(resource, "id") ?? "(no-id)";

            ValidatePatientReference(resource, patientId, "subject", type, id, errors);
            ValidatePatientReference(resource, patientId, "patient", type, id, errors);
            ValidatePatientReference(resource, patientId, "beneficiary", type, id, errors);

            if (IsType(resource, "MeasureReport"))
            {
                var mrId = GetString(resource, "id");
                if (!string.IsNullOrWhiteSpace(mrId))
                    patientMeasureReportIds.Add(mrId);
            }
        }
    }

    private void ValidateManifestMeasureReportReferences(
        List<JsonElement> manifestResources,
        HashSet<string> patientMeasureReportIds,
        HashSet<string>? expectedSubmittedMeasureReportIds,
        List<string> errors)
    {
        var manifestReferencedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var mr in manifestResources.Where(r => IsType(r, "MeasureReport") && string.Equals(GetString(r, "type"), "subject-list", StringComparison.OrdinalIgnoreCase)))
        {
            if (!mr.TryGetProperty("contained", out var contained) || contained.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var containedRes in contained.EnumerateArray())
            {
                if (!IsType(containedRes, "List"))
                    continue;

                if (!containedRes.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var entry in entries.EnumerateArray())
                {
                    var reference = GetNestedString(entry, "item", "reference");
                    if (reference != null && reference.StartsWith("MeasureReport/", StringComparison.Ordinal))
                    {
                        manifestReferencedIds.Add(reference.Substring("MeasureReport/".Length));
                    }
                }
            }
        }

        foreach (var id in manifestReferencedIds)
        {
            var shouldRequireInSubmittedPatientArtifacts = expectedSubmittedMeasureReportIds == null
                || expectedSubmittedMeasureReportIds.Count == 0
                || expectedSubmittedMeasureReportIds.Contains(id);

            if (shouldRequireInSubmittedPatientArtifacts && !patientMeasureReportIds.Contains(id))
                AddError(errors, $"Manifest references MeasureReport/{id} but it was not found in patient artifacts.");
        }
    }

    private void ValidateGeneratedBundleReconciliation(
        IReadOnlyList<(string Name, string Json)> generatedBundles,
        List<AbsResourceRecord> patientResources,
        List<string> errors)
    {
        var generatedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, json) in generatedBundles)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("request", out var request) || request.ValueKind != JsonValueKind.Object)
                        continue;

                    var url = GetString(request, "url");
                    if (!string.IsNullOrWhiteSpace(url) && url.Contains('/'))
                        generatedKeys.Add(url);
                }
            }
            catch (Exception ex)
            {
                AddError(errors, $"Failed to parse generated bundle '{name}': {ex.Message}");
            }
        }

        var absNonDerivedKeys = patientResources
            .Where(r => !string.IsNullOrWhiteSpace(r.ResourceType) && !string.IsNullOrWhiteSpace(r.ResourceId))
            .Select(r => ToResourceKey(r.ResourceType, r.ResourceId))
            .Where(k =>
            {
                var resourceType = k.Split('/')[0];
                return !IsDerivedType(resourceType)
                    && !string.Equals(resourceType, "Organization", StringComparison.OrdinalIgnoreCase);
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingFromGenerated = absNonDerivedKeys
            .Except(generatedKeys, StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();

        foreach (var missing in missingFromGenerated)
            AddError(errors, $"ABS contains resource not present in generated FHIR bundles: {missing}");
    }

    /// <summary>
    /// Reconciliation of ABS against the in-memory <see cref="GenerationManifest"/>.
    /// Used when the caller has a manifest but did not retain serialized bundles (streaming pipeline).
    /// </summary>
    private void ValidateGeneratedManifestReconciliation(
        GenerationManifest manifest,
        List<AbsResourceRecord> patientResources,
        List<string> errors)
    {
        var generatedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, keys) in manifest.ResourceKeysByPatient)
            foreach (var key in keys)
                generatedKeys.Add(key);

        var absNonDerivedKeys = patientResources
            .Where(r => !string.IsNullOrWhiteSpace(r.ResourceType) && !string.IsNullOrWhiteSpace(r.ResourceId))
            .Select(r => ToResourceKey(r.ResourceType, r.ResourceId))
            .Where(k =>
            {
                var resourceType = k.Split('/')[0];
                return !IsDerivedType(resourceType)
                    && !string.Equals(resourceType, "Organization", StringComparison.OrdinalIgnoreCase);
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingFromGenerated = absNonDerivedKeys
            .Except(generatedKeys, StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();

        foreach (var missing in missingFromGenerated)
            AddError(errors, $"ABS contains resource not present in generation manifest: {missing}");
    }

    /// <summary>
    /// Validates that ABS patient artifacts contain the resources the pipeline should have
    /// produced, using the generation manifest as the source of truth.
    ///
    /// <b>How the system works:</b>
    /// <list type="number">
    ///   <item>We <b>generated</b> resources and uploaded them to the FHIR server.</item>
    ///   <item>Data Acquisition ran the query plan (Parameter + Reference queries) and
    ///         sent every acquired resource to MeasureEval via Kafka.</item>
    ///   <item>MeasureEval bundled all acquired resources as <c>additionalData</c> and
    ///         evaluated the CQL. The HAPI CQL engine places resources into
    ///         <c>MeasureReport.contained</c> only when they are touched by a CQL
    ///         <c>[ResourceType]</c> retrieve expression.</item>
    ///   <item>MeasureEval's <c>normalize()</c> extracts <c>contained</c> resources and
    ///         writes them (plus the MeasureReport) to a per-patient <c>.mr</c> file in ABS.</item>
    ///   <item>The Report service's PatientAggregator reads the <c>.mr</c> files, dedupes
    ///         resources across measures, and writes <c>patient-{id}.ndjson</c> to ABS.
    ///         It also saves the same resource references to the ReportResource DB.</item>
    /// </list>
    ///
    /// A resource type is expected in ABS when: we generated it AND the query plan acquires
    /// it AND the CQL references it. The system is deterministic - no tolerance is needed.
    ///
    /// Shared infrastructure resources are stored under the empty patient key in the manifest.
    /// They are excluded from per-patient key validation because their IDs are rewritten by
    /// MeasureEval's normalize step (the <c>#LCR-</c> prefix is stripped).
    /// </summary>
    private void ValidateAbsResourceCountsAgainstManifest(
        GenerationManifest manifest,
        List<AbsResourceRecord> parsedPatientResources,
        IReadOnlyCollection<string> expectedSubmittedPatientIds,
        List<string> errors)
    {
        var absCountsByPatientType = parsedPatientResources
            .Where(r => !string.IsNullOrWhiteSpace(r.ResourceType) && !string.IsNullOrWhiteSpace(r.ResourceId))
            .GroupBy(r => r.PatientId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(r => r.ResourceType, StringComparer.OrdinalIgnoreCase)
                      .ToDictionary(rg => rg.Key, rg => rg.Count(), StringComparer.OrdinalIgnoreCase),
                StringComparer.Ordinal);

        foreach (var patientId in expectedSubmittedPatientIds)
        {
            var manifestExpectedCounts = manifest.GetExpectedAbsCountsForPatient(patientId);
            if (manifestExpectedCounts == null)
                continue;

            var expectedCounts = new Dictionary<string, int>(manifestExpectedCounts, StringComparer.OrdinalIgnoreCase);

            absCountsByPatientType.TryGetValue(patientId, out var actualCounts);
            actualCounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Strict prediction-vs-actual: every expected type must match exactly, and any
            // type actually produced must have been predicted. Pipeline-derived types
            // (MeasureReport, OperationOutcome) now contribute deterministic expectations
            // via GenerationManifest.GetExpectedAbsCountsForPatient, so no tolerance is needed.
            var allTypes = new HashSet<string>(expectedCounts.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var t in actualCounts.Keys) allTypes.Add(t);

            foreach (var resourceType in allTypes)
            {
                expectedCounts.TryGetValue(resourceType, out var expectedCount);
                actualCounts.TryGetValue(resourceType, out var actualCount);

                if (actualCount == expectedCount)
                    continue;

                if (actualCount < expectedCount)
                {
                    AddError(errors,
                        $"ABS patient={patientId}, type={resourceType}: " +
                        $"expected={expectedCount} (sim-acquired ? reachable-CQL + derived), actual={actualCount}.");
                }
                else
                {
                    AddError(errors,
                        $"ABS patient={patientId}, type={resourceType}: " +
                        $"expected={expectedCount}, actual={actualCount} (ABS has {actualCount - expectedCount} more than predicted).");
                }
            }
        }

        // Validate that every expected patient-scoped resource key is present in ABS.
        // Shared infrastructure (empty patient key) is excluded - those resources reach ABS
        // through MeasureReport contained resources with MeasureEval-rewritten IDs.
        var allAbsKeys = parsedPatientResources
            .Where(r => !string.IsNullOrWhiteSpace(r.ResourceType) && !string.IsNullOrWhiteSpace(r.ResourceId))
            .Select(r => ToResourceKey(r.ResourceType, r.ResourceId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Expected key-level ABS content must be scoped to the same terminal submitted-patient
        // set used for patient artifact expectations. Using all manifest-qualified patients here
        // causes false failures in scheduled runs where report entries resolve to NotReportable
        // and no patient-{id}.ndjson is produced.
        var expectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var patientId in expectedSubmittedPatientIds)
        {
            foreach (var key in manifest.GetExpectedAbsKeysForPatient(patientId))
                expectedKeys.Add(key);
        }

        var missing = expectedKeys
            .Where(k => !allAbsKeys.Contains(k))
            .ToList();

        foreach (var key in missing.Take(MaxErrors))
            AddError(errors, $"ABS artifacts missing expected resource: {key}");
    }

    private void ValidateManifestPatientList(
        JsonElement patientList,
        IReadOnlyCollection<string> expectedPatientIds,
        string expectedStartDate,
        string expectedEndDate,
        List<string> errors)
    {
        var status = GetString(patientList, "status");
        var mode = GetString(patientList, "mode");

        if (!string.Equals(status, "current", StringComparison.OrdinalIgnoreCase))
            AddError(errors, $"Manifest patient List.status expected 'current', actual '{status ?? "(null)"}'.");

        if (!string.Equals(mode, "snapshot", StringComparison.OrdinalIgnoreCase))
            AddError(errors, $"Manifest patient List.mode expected 'snapshot', actual '{mode ?? "(null)"}'.");

        if (!patientList.TryGetProperty("extension", out var extArr) || extArr.ValueKind != JsonValueKind.Array)
        {
            AddError(errors, "Manifest patient List is missing extension array.");
            return;
        }

        var applicablePeriodExtension = extArr.EnumerateArray()
            .FirstOrDefault(e => string.Equals(GetString(e, "url"), ApplicablePeriodExtensionUrl, StringComparison.Ordinal));

        if (applicablePeriodExtension.ValueKind == JsonValueKind.Undefined)
        {
            AddError(errors, "Manifest patient List is missing applicable-period extension.");
        }
        else
        {
            var periodStart = GetNestedString(applicablePeriodExtension, "valuePeriod", "start");
            var periodEnd = GetNestedString(applicablePeriodExtension, "valuePeriod", "end");

            if (!AreSameInstant(periodStart, expectedStartDate))
                AddError(errors, $"Manifest patient List applicable period.start mismatch. Expected '{expectedStartDate}', actual '{periodStart ?? "(null)"}'");

            if (!AreSameInstant(periodEnd, expectedEndDate))
                AddError(errors, $"Manifest patient List applicable period.end mismatch. Expected '{expectedEndDate}', actual '{periodEnd ?? "(null)"}'");
        }

        var listedPatients = GetPatientIdsFromList(patientList);
        var expected = expectedPatientIds.ToHashSet(StringComparer.Ordinal);

        foreach (var pid in expected)
        {
            if (!listedPatients.Contains(pid))
                AddError(errors, $"Manifest patient list missing expected patient {pid}.");
        }

        foreach (var pid in listedPatients)
        {
            if (!expected.Contains(pid))
                AddError(errors, $"Manifest patient list contains unexpected patient {pid}.");
        }
    }

    private List<JsonElement> ParseNdjson(string ndjson, string fileName, List<string> errors)
    {
        var resources = new List<JsonElement>();
        var lines = ndjson.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
                continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement.Clone();
                var type = GetString(root, "resourceType");
                if (string.IsNullOrWhiteSpace(type))
                    AddError(errors, $"{fileName} line {i + 1}: missing resourceType.");

                resources.Add(root);
            }
            catch (Exception ex)
            {
                AddError(errors, $"{fileName} line {i + 1}: invalid JSON ({ex.Message}).");
            }
        }

        return resources;
    }

    private static bool IsType(JsonElement resource, string type) =>
        string.Equals(GetString(resource, "resourceType"), type, StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> GetPatientIdsFromList(JsonElement listResource)
    {
        var patientIds = new HashSet<string>(StringComparer.Ordinal);
        if (!listResource.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            return patientIds;

        foreach (var entry in entries.EnumerateArray())
        {
            var reference = GetNestedString(entry, "item", "reference");
            if (reference != null && reference.StartsWith("Patient/", StringComparison.Ordinal))
                patientIds.Add(reference.Substring("Patient/".Length));
        }

        return patientIds;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return null;

        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }

    private static string? GetNestedString(JsonElement element, string parent, string child)
    {
        if (!element.TryGetProperty(parent, out var parentProp) || parentProp.ValueKind != JsonValueKind.Object)
            return null;

        return GetString(parentProp, child);
    }

    private static void ValidatePatientReference(
        JsonElement resource,
        string expectedPatientId,
        string referenceContainerProperty,
        string resourceType,
        string resourceId,
        List<string> errors)
    {
        var reference = GetNestedString(resource, referenceContainerProperty, "reference");
        if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith("Patient/", StringComparison.Ordinal))
            return;

        var actualPatientId = reference.Substring("Patient/".Length);
        if (!string.Equals(actualPatientId, expectedPatientId, StringComparison.Ordinal))
            AddError(errors, $"Resource {resourceType}/{resourceId} has {referenceContainerProperty}.reference={reference}, expected Patient/{expectedPatientId}.");
    }

    private async Task FailIfNeededAsync(List<string> errors)
    {
        if (errors.Count == 0)
        {
            _output.WriteLine("REPORT INTERNAL ABS MANIFEST VALIDATION: Passed");
            return;
        }

        _output.WriteLine($"REPORT INTERNAL ABS MANIFEST VALIDATION: Failed ({errors.Count} issue(s))");
        foreach (var error in errors.Take(MaxErrors))
            _output.WriteLine($"  - {error}");

        if (errors.Count > MaxErrors)
            _output.WriteLine($"  - Additional issues omitted: {errors.Count - MaxErrors}");

        await Task.CompletedTask;
        var sampleIssues = errors
            .Take(3)
            .Select(e => $"- {e}")
            .ToList();

        var sampleSuffix = errors.Count > sampleIssues.Count
            ? $" | ... and {errors.Count - sampleIssues.Count} more"
            : string.Empty;

        await Task.CompletedTask;
        throw new InvalidOperationException(
            $"REPORT INTERNAL ABS MANIFEST VALIDATION failed with {errors.Count} issue(s): {string.Join(" | ", sampleIssues)}{sampleSuffix}");
    }

    private static void AddError(List<string> errors, string message)
    {
        if (errors.Count < MaxErrors)
            errors.Add(message);
    }

    private static string ToResourceKey(string type, string id) => $"{type}/{id}";

    private static bool IsDerivedType(string resourceType) =>
        DerivedResourceTypes.Contains(resourceType, StringComparer.OrdinalIgnoreCase);

    private static bool IsReadyForValidation(string? status) =>
        string.Equals(status, "ReadyForValidation", StringComparison.OrdinalIgnoreCase);

    private sealed record AbsResourceRecord(string SourceFile, string PatientId, string ResourceType, string ResourceId, JsonElement Resource);
}
