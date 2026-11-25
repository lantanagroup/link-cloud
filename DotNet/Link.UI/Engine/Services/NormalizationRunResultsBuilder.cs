using System.Text.Json;
using Automation.UI.Models;
using LantanaGroup.Automation.Generation;

namespace Automation.UI.Services;

/// <summary>
/// Maps a run's stored configuration, manifest, and normalization evidence into the
/// run-details view. Older runs that lack a field are reported as not configured
/// or as having no evidence. They are not treated as failures.
/// </summary>
public static class NormalizationRunResultsBuilder
{
    public static NormalizationRunResults Build(NormalizationRunResultsSource source)
    {
        var config = RunConfigurationReader.Read(source.RunConfigurationJson);
        var (generated, uploaded) = ResolveCounts(source.Manifest, config);
        var patientSource = DescribeSource(generated, uploaded);

        var suiteId = source.Manifest?.AppliedNormalizationSuiteId ?? config.SuiteId;
        var evidence = source.Evidence;
        var hasRecordedSequences = evidence?.SuiteSequences is { Count: > 0 };
        var suiteName = hasRecordedSequences
            ? FirstName(evidence?.SuiteName, source.Manifest?.AppliedNormalizationSuiteName, source.Suite?.Name)
            : FirstName(source.Suite?.Name, source.Manifest?.AppliedNormalizationSuiteName, evidence?.SuiteName);
        var suiteState = ResolveArtifactState(suiteId, suiteName, source.Suite != null || hasRecordedSequences);

        var mapId = source.Manifest?.AppliedOrganizationResourceMapTemplateId ?? config.OrganizationMapId;
        var mapName = FirstName(source.OrganizationMap?.Name, source.Manifest?.AppliedOrganizationResourceMapName);
        var mapState = ResolveArtifactState(mapId, mapName, source.OrganizationMap != null);

        var displaySuite = suiteState == "not-configured"
            ? null
            : suiteName ?? "Selected suite";
        var lineCount = evidence?.CollectedLineCount ?? 0;
        var parsedCount = evidence?.ParsedSteps?.Count ?? 0;
        if (parsedCount == 0)
            parsedCount = evidence?.OmittedStepCount ?? 0;
        var inventory = evidence?.EvidenceInventory?.Where(line => !string.IsNullOrWhiteSpace(line)).ToList()
            ?? [];
        if (inventory.Count == 0 && evidence?.ParsedSteps is { Count: > 0 })
            inventory = NormalizationDiagnosticsWriter.FormatEvidenceInventory(evidence.ParsedSteps).ToList();

        var evidenceFound = lineCount > 0 || parsedCount > 0 || inventory.Count > 0;
        var evidencePresent = evidence != null;
        string evidenceState;
        string evidenceMessage;
        if (evidenceFound)
        {
            evidenceState = "found";
            evidenceMessage = $"{lineCount} normalization summary log line(s), {parsedCount} execution step(s).";
        }
        else if (!evidencePresent && !source.RunFinished)
        {
            evidenceState = "pending";
            evidenceMessage = "Normalization evidence has not been collected yet.";
        }
        else
        {
            evidenceState = "none";
            evidenceMessage = "No normalization execution evidence was found.";
        }

        var generatedState = "not-applicable";
        var generatedMessage = "This run did not use generated patients.";
        if (generated > 0)
        {
            var confirmed = source.Manifest?.GeneratedWithSelectedSuite == true
                || evidence?.GeneratorAppliedSelectedSuite == true;
            if (confirmed && displaySuite != null)
            {
                generatedState = "confirmed";
                generatedMessage =
                    $"The FHIR generator produced {generated} patient(s) with normalization suite '{displaySuite}'.";
            }
            else if (confirmed)
            {
                generatedState = "confirmed";
                generatedMessage =
                    $"The FHIR generator produced {generated} patient(s) with the selected normalization suite.";
            }
            else
            {
                generatedState = "not-recorded";
                generatedMessage =
                    "No confirmation was recorded that generated patient data was produced with the selected normalization suite.";
            }
        }

        var uploadedSpecific = evidenceFound && UploadedEvidenceIsSpecific(source.Manifest, evidence, generated);
        var uploadedState = "not-applicable";
        var uploadedMessage = "This run did not use uploaded patients.";
        if (uploaded > 0)
        {
            if (uploadedSpecific && displaySuite != null)
            {
                uploadedState = "confirmed";
                uploadedMessage = generated == 0
                    ? $"The uploaded-patient workflow used normalization suite '{displaySuite}' during processing ({lineCount} summary log line(s))."
                    : $"Normalization evidence for the uploaded patients used suite '{displaySuite}'.";
            }
            else if (uploadedSpecific)
            {
                uploadedState = "confirmed";
                uploadedMessage = generated == 0
                    ? $"The uploaded-patient workflow used the selected normalization suite during processing ({lineCount} summary log line(s))."
                    : "Normalization evidence for the uploaded patients used the selected normalization suite.";
            }
            else if (!source.RunFinished && !evidencePresent)
            {
                uploadedState = "pending";
                uploadedMessage = "Normalization evidence has not been collected yet.";
            }
            else if (!source.RunFinished)
            {
                uploadedState = "pending";
                uploadedMessage = "Normalization evidence for the uploaded patients is not confirmed yet.";
            }
            else if (evidenceFound)
            {
                uploadedState = "no-evidence";
                uploadedMessage = "Normalization evidence was found, but none of it is tied to the uploaded patients.";
            }
            else
            {
                uploadedState = "no-evidence";
                uploadedMessage = "No normalization summary log evidence was found for the uploaded-patient workflow.";
            }
        }

        NormalizationSuiteSummary? suiteSummary = null;
        var suiteDefinitionSource = string.Empty;
        var suiteDefinitionNote = string.Empty;
        if (hasRecordedSequences && evidence != null)
        {
            suiteSummary = BuildRecordedSuite(suiteId, displaySuite, evidence.SuiteSequences);
            suiteDefinitionSource = "snapshot";
            suiteDefinitionNote = "These sequences were stored with this run.";
        }
        else if (source.Suite != null)
        {
            suiteSummary = NormalizationSuiteSummaryBuilder.Build(
                source.Suite,
                source.SuiteOperations ?? [],
                source.SuiteSequences ?? []);
            suiteDefinitionSource = "current";
            suiteDefinitionNote = source.SuiteChangedAfterRun
                ? "This suite was modified after the run started. The sequences shown are the current definition, not what this run used."
                : "This is the current suite definition, not a snapshot from this run.";
        }

        var operations = new List<NormalizationOperationSummaryLine>();
        if (evidence?.OperationConfigs is { Count: > 0 })
        {
            operations.AddRange(evidence.OperationConfigs.Select(op => new NormalizationOperationSummaryLine
            {
                Name = op.Name,
                OperationType = op.OperationType,
                ResourceTypes = op.ResourceTypes ?? []
            }));
        }
        else if (suiteSummary != null)
        {
            foreach (var sequence in suiteSummary.Sequences)
            {
                operations.AddRange(sequence.Operations.Where(op => !op.Missing).Select(op => new NormalizationOperationSummaryLine
                {
                    Name = op.Name,
                    OperationType = op.OperationType,
                    ResourceTypes = op.ResourceTypes
                }));
            }

            operations.AddRange(suiteSummary.Operations.Where(op => !op.Missing).Select(op => new NormalizationOperationSummaryLine
            {
                Name = op.Name,
                OperationType = op.OperationType,
                ResourceTypes = op.ResourceTypes
            }));
        }

        return new NormalizationRunResults
        {
            SuiteState = suiteState,
            SuiteId = suiteState == "not-configured" ? null : suiteId,
            SuiteName = displaySuite,
            Suite = suiteSummary,
            SuiteDefinitionSource = suiteDefinitionSource,
            SuiteDefinitionNote = suiteDefinitionNote,
            OrganizationMapState = mapState,
            OrganizationMapId = mapState == "not-configured" ? null : mapId,
            OrganizationMapName = mapState == "not-configured" ? null : mapName ?? "Selected organization map",
            OrganizationMap = source.OrganizationMap == null
                ? null
                : NormalizationSuiteSummaryBuilder.Build(source.OrganizationMap),
            PatientSource = patientSource,
            GeneratedPatientCount = generated,
            UploadedPatientCount = uploaded,
            GeneratedConfirmationState = generatedState,
            GeneratedConfirmation = generatedMessage,
            UploadedConfirmationState = uploadedState,
            UploadedConfirmation = uploadedMessage,
            EvidenceState = evidenceState,
            EvidenceMessage = evidenceMessage,
            EvidenceLineCount = lineCount,
            ParsedStepCount = parsedCount,
            EvidenceInventory = inventory,
            Operations = operations
        };
    }

    private static (int Generated, int Uploaded) ResolveCounts(
        GenerationManifestSnapshot? manifest,
        RunConfigurationFacts config)
    {
        if (manifest?.PatientOriginByPatient is { Count: > 0 })
        {
            var generated = manifest.PatientOriginByPatient.Values.Count(value =>
                string.Equals(value, PatientOrigin.Generated, StringComparison.Ordinal));
            var uploaded = manifest.PatientOriginByPatient.Values.Count(value =>
                string.Equals(value, PatientOrigin.Uploaded, StringComparison.Ordinal));
            return (generated, uploaded);
        }

        return (Math.Max(0, config.GeneratedPatientCount), Math.Max(0, config.UploadedPatientCount));
    }

    private static string DescribeSource(int generated, int uploaded)
    {
        if (generated > 0 && uploaded > 0)
            return "mixed";
        if (generated > 0)
            return "generated";
        if (uploaded > 0)
            return "uploaded";
        return "unknown";
    }

    private static string ResolveArtifactState(Guid? id, string? name, bool loaded)
    {
        var named = !string.IsNullOrWhiteSpace(name)
            && !string.Equals(name, "None", StringComparison.OrdinalIgnoreCase);
        if (!id.HasValue || id.Value == Guid.Empty)
            return named ? "configured" : "not-configured";
        if (loaded)
            return "configured";
        return "unavailable";
    }

    /// <summary>
    /// A non-system suite, sequence, or operation saved after the run started means the
    /// stored definition may differ from what ran. System records are rewritten on startup,
    /// so their timestamps are not treated as an edit.
    /// </summary>
    public static bool SuiteDefinitionChangedAfter(
        NormalizationSuiteDefinition suite,
        IReadOnlyList<NormalizationOperationDefinition>? operations,
        IReadOnlyList<NormalizationSequenceDefinition>? sequences,
        DateTimeOffset runStarted)
    {
        if (!suite.IsSystem && suite.UpdatedAt > runStarted)
            return true;

        var operationIds = new HashSet<Guid>(suite.OperationIds);
        if (sequences != null)
        {
            var sequenceIds = new HashSet<Guid>(suite.SequenceIds);
            foreach (var sequence in sequences)
            {
                if (!sequenceIds.Contains(sequence.Id))
                    continue;
                if (!sequence.IsSystem && sequence.UpdatedAt > runStarted)
                    return true;
                foreach (var entry in sequence.Entries)
                    operationIds.Add(entry.OperationId);
            }
        }

        if (operations == null)
            return false;

        foreach (var operation in operations)
        {
            if (operationIds.Contains(operation.Id)
                && !operation.IsSystem
                && operation.UpdatedAt > runStarted)
                return true;
        }

        return false;
    }

    private static bool UploadedEvidenceIsSpecific(
        GenerationManifestSnapshot? manifest,
        NormalizationEvidenceSnapshot? evidence,
        int generated)
    {
        if (generated == 0)
            return true;
        if (evidence == null)
            return false;

        var uploadedIds = new HashSet<string>(StringComparer.Ordinal);
        if (manifest?.PatientOriginByPatient != null)
        {
            foreach (var pair in manifest.PatientOriginByPatient)
            {
                if (string.Equals(pair.Value, PatientOrigin.Uploaded, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(pair.Key))
                    uploadedIds.Add(pair.Key);
            }
        }

        if (uploadedIds.Count == 0)
            return false;

        if (ContainsUploadedPatient(evidence.EvidencePatientIds, uploadedIds))
            return true;

        if (evidence.ParsedSteps != null)
        {
            foreach (var step in evidence.ParsedSteps)
            {
                if (MatchesUploadedPatient(step.PatientId, step.ResourceType, step.ResourceId, uploadedIds))
                    return true;
            }
        }

        if (evidence.SummaryLines is { Count: > 0 })
        {
            foreach (var step in NormalizationExecutionSummaryParser.ParseAll(evidence.SummaryLines))
            {
                if (MatchesUploadedPatient(step.PatientId, step.ResourceType, step.ResourceId, uploadedIds))
                    return true;
            }
        }

        return false;
    }

    private static bool ContainsUploadedPatient(IEnumerable<string>? ids, HashSet<string> uploadedIds)
    {
        if (ids == null)
            return false;
        foreach (var id in ids)
        {
            if (!string.IsNullOrWhiteSpace(id) && uploadedIds.Contains(id.Trim()))
                return true;
        }

        return false;
    }

    private static bool MatchesUploadedPatient(
        string? patientId,
        string? resourceType,
        string? resourceId,
        HashSet<string> uploadedIds)
    {
        if (!string.IsNullOrWhiteSpace(patientId) && uploadedIds.Contains(patientId.Trim()))
            return true;
        return string.Equals(resourceType, "Patient", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(resourceId)
            && uploadedIds.Contains(resourceId.Trim());
    }

    private static NormalizationSuiteSummary BuildRecordedSuite(
        Guid? suiteId,
        string? suiteName,
        IReadOnlyList<NormalizationSuiteSequenceStep> steps)
    {
        var sequences = new List<NormalizationSuiteSummarySequence>();
        var standalone = new List<NormalizationSuiteSummaryOperation>();
        foreach (var group in steps.GroupBy(step => step.SequenceName ?? string.Empty))
        {
            var operations = group
                .OrderBy(step => step.Sequence)
                .Select(step => new NormalizationSuiteSummaryOperation
                {
                    Sequence = step.Sequence,
                    Name = step.OperationName,
                    OperationType = step.OperationType,
                    ResourceTypes = step.ResourceTypes ?? []
                })
                .ToList();
            if (string.Equals(group.Key, "(standalone)", StringComparison.OrdinalIgnoreCase))
            {
                standalone.AddRange(operations);
                continue;
            }

            sequences.Add(new NormalizationSuiteSummarySequence
            {
                Name = string.IsNullOrWhiteSpace(group.Key) ? "Sequence" : group.Key,
                Operations = operations
            });
        }

        return new NormalizationSuiteSummary
        {
            Id = suiteId ?? Guid.Empty,
            Name = suiteName ?? string.Empty,
            Sequences = sequences,
            Operations = standalone
        };
    }

    private static string? FirstName(params string?[] names)
    {
        foreach (var name in names)
        {
            if (!string.IsNullOrWhiteSpace(name)
                && !string.Equals(name, "None", StringComparison.OrdinalIgnoreCase))
                return name.Trim();
        }

        return null;
    }
}

public sealed class NormalizationRunResultsSource
{
    public string? RunConfigurationJson { get; init; }
    public bool RunFinished { get; init; }
    public GenerationManifestSnapshot? Manifest { get; init; }
    public NormalizationEvidenceSnapshot? Evidence { get; init; }
    public NormalizationSuiteDefinition? Suite { get; init; }
    public IReadOnlyList<NormalizationOperationDefinition>? SuiteOperations { get; init; }
    public IReadOnlyList<NormalizationSequenceDefinition>? SuiteSequences { get; init; }
    public OrganizationResourceMapTemplate? OrganizationMap { get; init; }
    public bool SuiteChangedAfterRun { get; init; }
}

internal static class RunConfigurationReader
{
    public static RunConfigurationFacts Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new RunConfigurationFacts(null, null, 0, 0);

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new RunConfigurationFacts(null, null, 0, 0);

            var root = document.RootElement;
            var generated = ReadInt(root, "patientCount", "PatientCount");
            var uploaded = ReadArrayCount(root, "importedPatientIds", "ImportedPatientIds")
                + ReadArrayCount(root, "importedPatientBundles", "ImportedPatientBundles");
            return new RunConfigurationFacts(
                ReadGuid(root, "normalizationSuiteId", "NormalizationSuiteId"),
                ReadGuid(root, "organizationResourceMapTemplateId", "OrganizationResourceMapTemplateId"),
                generated,
                uploaded);
        }
        catch (JsonException)
        {
            return new RunConfigurationFacts(null, null, 0, 0);
        }
    }

    private static int ReadInt(JsonElement root, string camel, string pascal)
    {
        if (!TryGet(root, camel, pascal, out var value))
            return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;
        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), out var parsed))
            return parsed;
        return 0;
    }

    private static int ReadArrayCount(JsonElement root, string camel, string pascal)
    {
        if (!TryGet(root, camel, pascal, out var value) || value.ValueKind != JsonValueKind.Array)
            return 0;
        return value.GetArrayLength();
    }

    private static Guid? ReadGuid(JsonElement root, string camel, string pascal)
    {
        if (!TryGet(root, camel, pascal, out var value))
            return null;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        return Guid.TryParse(text, out var id) && id != Guid.Empty ? id : null;
    }

    private static bool TryGet(JsonElement root, string camel, string pascal, out JsonElement value)
    {
        if (root.TryGetProperty(camel, out value))
            return true;
        return root.TryGetProperty(pascal, out value);
    }
}

internal readonly record struct RunConfigurationFacts(
    Guid? SuiteId,
    Guid? OrganizationMapId,
    int GeneratedPatientCount,
    int UploadedPatientCount);
