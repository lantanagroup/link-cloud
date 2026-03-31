using System.Text.Json;
using LantanaGroup.Link.Automation.Helpers;

namespace LantanaGroup.Link.Automation.Validation;

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

    private static readonly HashSet<string> AbsSupplementalResourceTypes =
    [
        "OperationOutcome"
    ];

    private readonly IAutomationOutput _output;
    private readonly PipelineDataReader _reader;

    public ReportAbsManifestValidator(IAutomationOutput output, PipelineDataReader reader)
    {
        _output = output;
        _reader = reader;
    }

    public async Task ValidateAllAsync(
        IDictionary<string, object> internalAbsResources,
        IReadOnlyCollection<string> expectedPatientIds,
        string expectedMeasureId,
        string expectedStartDate,
        string expectedEndDate,
        string? facilityId = null,
        string? reportId = null,
        string? generatedSnapshotDirectory = null,
        IReadOnlyCollection<string>? expectedManifestPatientListIds = null,
        bool expectDataAcquisitionData = true)
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
        ValidateManifest(manifestResources, expectedListPatientIds, expectedMeasureId, expectedStartDate, expectedEndDate, errors);

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

        ValidateManifestMeasureReportReferences(manifestResources, patientMeasureReportIds, errors);

        if (!string.IsNullOrWhiteSpace(facilityId) && !string.IsNullOrWhiteSpace(reportId))
        {
            await ValidateDatabaseReconciliationAsync(
                facilityId,
                reportId,
                actualPatientFiles,
                parsedPatientResources,
                errors,
                expectDataAcquisitionData);

            if (!string.IsNullOrWhiteSpace(generatedSnapshotDirectory))
            {
                ValidateGeneratedSnapshotReconciliation(generatedSnapshotDirectory, parsedPatientResources, errors);
            }
        }

        await FailIfNeededAsync(errors);
    }

    private void ValidateManifest(
        List<JsonElement> manifestResources,
        IReadOnlyCollection<string> expectedPatientIds,
        string expectedMeasureId,
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

        foreach (var mr in subjectListReports)
        {
            var measure = GetString(mr, "measure");
            if (string.IsNullOrWhiteSpace(measure) || !measure.Contains(expectedMeasureId, StringComparison.OrdinalIgnoreCase))
                AddError(errors, $"Manifest MeasureReport measure does not contain expected measure id '{expectedMeasureId}'. Actual='{measure ?? "(null)"}'");

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
        if (!patientResourceFound)
            AddError(errors, $"patient-{patientId}.ndjson does not contain Patient/{patientId}.");

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
            if (!patientMeasureReportIds.Contains(id))
                AddError(errors, $"Manifest references MeasureReport/{id} but it was not found in patient artifacts.");
        }
    }

    private async Task ValidateDatabaseReconciliationAsync(
        string facilityId,
        string reportId,
        IReadOnlySet<string> actualPatientFiles,
        List<AbsResourceRecord> patientResources,
        List<string> errors,
        bool expectDataAcquisitionData)
    {
        if (!Guid.TryParse(reportId, out var scheduleId))
        {
            AddError(errors, $"Invalid reportId format for reconciliation: {reportId}");
            return;
        }

        var reportResources = await _reader.GetReportResourceIdentitiesAsync(scheduleId, facilityId);
        if (reportResources.Count == 0)
        {
            AddError(errors, "Report DB has no ReportResource rows for this schedule/facility.");
            return;
        }

        var allEntries = await _reader.GetReportEntriesAsync(scheduleId);
        var entryByPatient = allEntries
            .GroupBy(e => e.PatientId)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var submittedEntries = await _reader.GetSubmittedReportEntriesAsync(scheduleId);
        var submittedPatients = submittedEntries.Select(e => e.PatientId).ToHashSet(StringComparer.Ordinal);
        if (submittedPatients.Count > 0)
        {
            var artifactPatients = actualPatientFiles
                .Select(f => f.Substring("patient-".Length, f.Length - "patient-".Length - ".ndjson".Length))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var patientId in submittedPatients)
            {
                if (!artifactPatients.Contains(patientId))
                    AddError(errors, $"Submitted ReportEntry patient {patientId} is missing a patient artifact file.");
            }
        }

        var patientsWithOperationOutcome = patientResources
            .Where(r => string.Equals(r.ResourceType, "OperationOutcome", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.PatientId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var patientId in patientsWithOperationOutcome)
        {
            if (!entryByPatient.TryGetValue(patientId, out var entry))
            {
                AddError(errors, $"Patient artifact for {patientId} contains OperationOutcome but no matching ReportEntry was found.");
                continue;
            }

            if (!string.Equals(entry.ReportingStatus, "FailedValidation", StringComparison.OrdinalIgnoreCase))
            {
                AddError(errors,
                    $"Patient artifact for {patientId} contains OperationOutcome but ReportEntry.ReportingStatus is {entry.ReportingStatus} (expected FailedValidation).");
            }
        }

        var expectedKeys = reportResources
            .Select(r => ToResourceKey(r.ResourceType, r.ResourceId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var absKeys = patientResources
            .Where(r => !string.IsNullOrWhiteSpace(r.ResourceType) && !string.IsNullOrWhiteSpace(r.ResourceId))
            .Select(r => ToResourceKey(r.ResourceType, r.ResourceId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var absComparableKeys = absKeys
            .Where(k => !IsAbsSupplementalType(GetResourceTypeFromKey(k)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingInAbs = expectedKeys.Except(absKeys, StringComparer.OrdinalIgnoreCase).Take(50).ToList();
        foreach (var missing in missingInAbs)
            AddError(errors, $"ABS patient artifacts are missing ReportResource {missing}.");

        var unexpectedInAbs = absComparableKeys.Except(expectedKeys, StringComparer.OrdinalIgnoreCase).Take(50).ToList();
        foreach (var extra in unexpectedInAbs)
            AddError(errors, $"ABS patient artifacts contain unexpected resource not present in ReportResource table: {extra}.");

        var reportCountMap = ToCountMap(
            (await _reader.GetReportResourceCountsByPatientTypeAsync(scheduleId, facilityId))
            .Select(x => (x.PatientId, x.ResourceType, x.Count)));

        var absCountMap = ToCountMap(
            patientResources
                .Where(r => !string.IsNullOrWhiteSpace(r.ResourceType) && !string.IsNullOrWhiteSpace(r.ResourceId) && !IsAbsSupplementalType(r.ResourceType))
                .GroupBy(r => new { r.PatientId, r.ResourceType })
                .Select(g => (g.Key.PatientId, g.Key.ResourceType, g.Count())));

        CompareCountMapsExact("ReportResource->ABS", reportCountMap, absCountMap, errors);

        var measureEvalCountMap = ToCountMap(
            (await _reader.GetMeasureEvalResourceCountsByPatientTypeAsync(scheduleId))
            .Select(x => (x.PatientId, x.ResourceType, x.Count)));

        CompareCountMapsExact("MeasureEval->ReportResource", measureEvalCountMap, reportCountMap, errors);
        CompareCountMapsExact("MeasureEval->ABS", measureEvalCountMap, absCountMap, errors);

        if (expectDataAcquisitionData)
        {
            var dataAcqCountMap = ToCountMap(
                (await _reader.GetDataAcquisitionResourceCountsByPatientTypeAsync(facilityId, reportId))
                .Where(x => submittedPatients.Contains(x.PatientId))
                .Select(x => (x.PatientId, x.ResourceType, x.Count)));

            CompareCountMapsMinimum("DataAcquisition->ReportResource", dataAcqCountMap, reportCountMap, errors);
            CompareCountMapsMinimum("DataAcquisition->ABS", dataAcqCountMap, absCountMap, errors);

            var acquisitionLogs = await _reader.GetAcquisitionLogsAsync(facilityId, reportId);
            var acquiredKeys = acquisitionLogs
                .Where(l => string.Equals(l.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                .SelectMany(l => l.ResourceAcquiredIds ?? [])
                .Where(r => !string.IsNullOrWhiteSpace(r) && r.Contains('/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var expectedNonDerived = expectedKeys
                .Where(k => !IsDerivedType(k.Split('/')[0]))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingInAcquisition = expectedNonDerived
                .Except(acquiredKeys, StringComparer.OrdinalIgnoreCase)
                .Take(50)
                .ToList();

            foreach (var missing in missingInAcquisition)
                AddError(errors, $"ReportResource {missing} not found in DataAcquisition completed ResourceAcquiredIds.");
        }
    }

    private void ValidateGeneratedSnapshotReconciliation(
        string generatedSnapshotDirectory,
        List<AbsResourceRecord> patientResources,
        List<string> errors)
    {
        if (!Directory.Exists(generatedSnapshotDirectory))
        {
            AddError(errors, $"Generated FHIR snapshot directory not found: {generatedSnapshotDirectory}");
            return;
        }

        var generatedFiles = Directory
            .EnumerateFiles(generatedSnapshotDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(f => !string.Equals(Path.GetFileName(f), "metadata.json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (generatedFiles.Count == 0)
        {
            AddError(errors, $"No generated bundle snapshot files found in {generatedSnapshotDirectory}");
            return;
        }

        var generatedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in generatedFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
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
                AddError(errors, $"Failed to parse generated snapshot file {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        var absNonDerivedKeys = patientResources
            .Where(r => !string.IsNullOrWhiteSpace(r.ResourceType) && !string.IsNullOrWhiteSpace(r.ResourceId))
            .Select(r => ToResourceKey(r.ResourceType, r.ResourceId))
            .Where(k => !IsDerivedType(k.Split('/')[0]))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingFromGenerated = absNonDerivedKeys
            .Except(generatedKeys, StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();

        foreach (var missing in missingFromGenerated)
            AddError(errors, $"ABS resource {missing} not found in generated FHIR input snapshot.");
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
        throw new InvalidOperationException($"REPORT INTERNAL ABS MANIFEST VALIDATION failed with {errors.Count} issue(s).");
    }

    private static void AddError(List<string> errors, string message)
    {
        if (errors.Count < MaxErrors)
            errors.Add(message);
    }

    private static string ToResourceKey(string type, string id) => $"{type}/{id}";

    private static string GetResourceTypeFromKey(string key)
    {
        var idx = key.IndexOf('/');
        return idx > 0 ? key[..idx] : key;
    }

    private static bool IsDerivedType(string resourceType) =>
        DerivedResourceTypes.Contains(resourceType, StringComparer.OrdinalIgnoreCase);

    private static bool IsAbsSupplementalType(string resourceType) =>
        AbsSupplementalResourceTypes.Contains(resourceType, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<(string PatientId, string ResourceType), int> ToCountMap(
        IEnumerable<(string PatientId, string ResourceType, int Count)> source)
    {
        var map = new Dictionary<(string PatientId, string ResourceType), int>();

        foreach (var row in source)
        {
            if (string.IsNullOrWhiteSpace(row.PatientId) || string.IsNullOrWhiteSpace(row.ResourceType))
                continue;

            var key = (row.PatientId, row.ResourceType);
            map[key] = map.TryGetValue(key, out var existing) ? existing + row.Count : row.Count;
        }

        return map;
    }

    private static void CompareCountMapsExact(
        string sourceName,
        Dictionary<(string PatientId, string ResourceType), int> expected,
        Dictionary<(string PatientId, string ResourceType), int> actual,
        List<string> errors)
    {
        var keys = expected.Keys.Union(actual.Keys).ToList();

        foreach (var key in keys)
        {
            expected.TryGetValue(key, out var expectedCount);
            actual.TryGetValue(key, out var actualCount);

            if (expectedCount != actualCount)
            {
                AddError(errors,
                    $"{sourceName} count mismatch for patient={key.PatientId}, type={key.ResourceType}: expected={expectedCount}, actual={actualCount}.");
            }
        }
    }

    private static void CompareCountMapsMinimum(
        string sourceName,
        Dictionary<(string PatientId, string ResourceType), int> minimumExpected,
        Dictionary<(string PatientId, string ResourceType), int> actual,
        List<string> errors)
    {
        foreach (var (key, minCount) in minimumExpected)
        {
            if (!actual.TryGetValue(key, out var actualCount))
            {
                AddError(errors,
                    $"{sourceName} missing patient/type in downstream layer: patient={key.PatientId}, type={key.ResourceType}, expectedAtLeast={minCount}, actual=0.");
                continue;
            }

            if (actualCount > minCount)
            {
                AddError(errors,
                    $"{sourceName} upstream count lower than downstream for patient={key.PatientId}, type={key.ResourceType}: upstream={minCount}, downstream={actualCount}.");
            }
        }
    }

    private sealed record AbsResourceRecord(string SourceFile, string PatientId, string ResourceType, string ResourceId, JsonElement Resource);
}
