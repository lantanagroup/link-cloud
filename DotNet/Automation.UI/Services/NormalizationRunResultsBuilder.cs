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
        var suiteName = FirstName(
            source.Suite?.Name,
            source.Manifest?.AppliedNormalizationSuiteName,
            source.Evidence?.SuiteName);
        var suiteState = ResolveArtifactState(suiteId, suiteName, source.Suite != null);

        var mapId = source.Manifest?.AppliedOrganizationResourceMapTemplateId ?? config.OrganizationMapId;
        var mapName = FirstName(source.OrganizationMap?.Name, source.Manifest?.AppliedOrganizationResourceMapName);
        var mapState = ResolveArtifactState(mapId, mapName, source.OrganizationMap != null);

        var displaySuite = suiteState == "not-configured"
            ? null
            : suiteName ?? "Selected suite";
        var evidence = source.Evidence;
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

        var uploadedState = "not-applicable";
        var uploadedMessage = "This run did not use uploaded patients.";
        if (uploaded > 0)
        {
            if (evidenceFound && displaySuite != null)
            {
                uploadedState = "confirmed";
                uploadedMessage =
                    $"The uploaded-patient workflow used normalization suite '{displaySuite}' during processing ({lineCount} summary log line(s)).";
            }
            else if (evidenceFound)
            {
                uploadedState = "confirmed";
                uploadedMessage =
                    $"The uploaded-patient workflow used the selected normalization suite during processing ({lineCount} summary log line(s)).";
            }
            else if (!source.RunFinished && !evidencePresent)
            {
                uploadedState = "pending";
                uploadedMessage = "Normalization evidence has not been collected yet.";
            }
            else
            {
                uploadedState = "no-evidence";
                uploadedMessage = "No normalization summary log evidence was found for the uploaded-patient workflow.";
            }
        }

        NormalizationSuiteSummary? suiteSummary = null;
        if (source.Suite != null)
        {
            suiteSummary = NormalizationSuiteSummaryBuilder.Build(
                source.Suite,
                source.SuiteOperations ?? [],
                source.SuiteSequences ?? []);
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
