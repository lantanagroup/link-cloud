using System.Text.Json;
using Automation.UI.Models;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace Automation.UI.Services;

/// <summary>
/// Reduces pipeline payloads to the counts, timestamps, and milestones the run
/// page charts need. Notes, acquired resource ids, FHIR queries, measure-report
/// id lists, per-patient measure rows, org-location rows, and normalization
/// execution lines stay in Report, Data Acquisition, and the Normalization service.
/// </summary>
public static class RunHistorySlim
{
    public static List<PipelineDataReader.AcquisitionLogInfo> SlimAcquisitionLogs(
        IReadOnlyList<PipelineDataReader.AcquisitionLogInfo>? logs)
    {
        if (logs == null || logs.Count == 0)
            return [];

        return logs.Select(log => log with
        {
            Notes = [],
            ResourceAcquiredIds = [],
            FhirQueries = []
        }).ToList();
    }

    public static PipelineDataReader.PopulationCountSnapshot ToPopulationCounts(
        IReadOnlyList<PipelineDataReader.ReportPopulationInfo>? populations)
    {
        if (populations == null || populations.Count == 0)
            return new PipelineDataReader.PopulationCountSnapshot(0, 0, 0);

        var groupCount = 0;
        var measureReportCount = 0;
        foreach (var population in populations)
        {
            var groups = population.GroupPopulations;
            if (groups == null)
                continue;

            groupCount += groups.Count;
            foreach (var group in groups)
                measureReportCount += group.MeasureReportPopulations?.Count ?? 0;
        }

        return new PipelineDataReader.PopulationCountSnapshot(populations.Count, groupCount, measureReportCount);
    }

    public static List<PipelineDataReader.PatientResourceTypeCount> SlimMeasureResources(
        IReadOnlyList<PipelineDataReader.PatientResourceTypeCount>? rows)
    {
        if (rows == null || rows.Count == 0)
            return [];

        return rows
            .GroupBy(row => row.ResourceType ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PipelineDataReader.PatientResourceTypeCount(
                string.Empty,
                group.Key,
                group.Sum(row => row.Count)))
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.ResourceType, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static OrgLocationSummary SlimOrgLocation(StoreBackedServicePoller.OrgLocationSnapshot? snapshot)
    {
        if (snapshot == null)
            return new OrgLocationSummary();

        var configurations = snapshot.Configurations ?? [];
        var mappings = snapshot.LocationMappings ?? [];
        var encounters = snapshot.EncounterMappings ?? [];
        return new OrgLocationSummary
        {
            ConfigurationCount = configurations.Count,
            ActiveConfigurationCount = configurations.Count(configuration => configuration.IsActive),
            LocationMappingCount = mappings.Count,
            OrgLocationMappingCount = mappings.Count(mapping => mapping.IsOrgLocation),
            EncounterMappingCount = encounters.Count,
            MappedToOrgCount = encounters.Count(mapping => mapping.MappedToOrg)
        };
    }

    public static NormalizationEvidenceSnapshot SlimNormalizationEvidence(NormalizationEvidenceSnapshot source)
    {
        return new NormalizationEvidenceSnapshot
        {
            SuiteName = source.SuiteName,
            CollectedLineCount = source.CollectedLineCount,
            RawLinesOmitted = true,
            StepsCollapsed = true,
            EvidenceChunkCount = 0,
            RuntimeSequences = source.RuntimeSequences,
            SuiteSequences = source.SuiteSequences,
            OperationConfigs = (source.OperationConfigs ?? [])
                .Select(operation => new NormalizationOperationConfigSnapshot
                {
                    Name = operation.Name,
                    OperationType = operation.OperationType,
                    ResourceTypes = operation.ResourceTypes
                })
                .ToList()
        };
    }

    /// <summary>
    /// Rewrites one stored payload to the summary shape. Returns null when the
    /// JSON is already that shape, or when it is not the pipeline document
    /// this domain used to store. Callers keep those documents as they are.
    /// </summary>
    public static string? TrySlimStoredJson(string? domain, string? json)
    {
        if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(json))
            return null;

        if (domain is not ("acquisitionLogs" or "populations" or "measureResources" or "orgLocation" or "normalizationEvidence"))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return domain switch
            {
                "acquisitionLogs" => SlimStoredAcquisitionLogs(root),
                "populations" => SlimStoredPopulations(root),
                "measureResources" => SlimStoredMeasureResources(root),
                "orgLocation" => SlimStoredOrgLocation(root),
                "normalizationEvidence" => SlimStoredNormalization(json, root),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool IsNormalizationEvidenceChunkDomain(string? domain)
        => !string.IsNullOrEmpty(domain)
           && domain.StartsWith(NormalizationEvidenceSnapshot.Domain + "-chunk-", StringComparison.Ordinal);

    private static string? SlimStoredAcquisitionLogs(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            return null;

        var fat = false;
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || (!item.TryGetProperty("Notes", out _)
                    && !item.TryGetProperty("ResourceAcquiredIds", out _)
                    && !item.TryGetProperty("FhirQueries", out _)))
            {
                return null;
            }

            if (HasItems(item, "Notes") || HasItems(item, "ResourceAcquiredIds") || HasItems(item, "FhirQueries"))
                fat = true;
        }

        if (!fat)
            return null;

        var logs = JsonSerializer.Deserialize<List<PipelineDataReader.AcquisitionLogInfo>>(root.GetRawText());
        return logs == null ? null : JsonSerializer.Serialize(SlimAcquisitionLogs(logs));
    }

    private static string? SlimStoredPopulations(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            return null;

        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("GroupPopulations", out _))
                return null;
        }

        var populations = JsonSerializer.Deserialize<List<PipelineDataReader.ReportPopulationInfo>>(root.GetRawText());
        return populations == null ? null : JsonSerializer.Serialize(ToPopulationCounts(populations));
    }

    private static string? SlimStoredMeasureResources(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            return null;

        var fat = false;
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("PatientId", out var patientId)
                || !item.TryGetProperty("ResourceType", out _)
                || !item.TryGetProperty("Count", out _))
            {
                return null;
            }

            if (patientId.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(patientId.GetString()))
                fat = true;
        }

        if (!fat)
            return null;

        var rows = JsonSerializer.Deserialize<List<PipelineDataReader.PatientResourceTypeCount>>(root.GetRawText());
        return rows == null ? null : JsonSerializer.Serialize(SlimMeasureResources(rows));
    }

    private static string? SlimStoredOrgLocation(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        if (!root.TryGetProperty("Configurations", out _)
            && !root.TryGetProperty("LocationMappings", out _)
            && !root.TryGetProperty("EncounterMappings", out _))
        {
            return null;
        }

        var snapshot = JsonSerializer.Deserialize<StoreBackedServicePoller.OrgLocationSnapshot>(root.GetRawText());
        return snapshot == null ? null : JsonSerializer.Serialize(SlimOrgLocation(snapshot));
    }

    private static string? SlimStoredNormalization(string json, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        if (!root.TryGetProperty("SuiteName", out _) && !root.TryGetProperty("CollectedLineCount", out _))
            return null;

        if (!NormalizationHasDroppedFields(root))
            return null;

        var evidence = JsonSerializer.Deserialize<NormalizationEvidenceSnapshot>(json);
        return evidence == null ? null : JsonSerializer.Serialize(SlimNormalizationEvidence(evidence));
    }

    private static bool NormalizationHasDroppedFields(JsonElement root)
    {
        if (HasItems(root, "SummaryLines") || HasItems(root, "ParsedSteps"))
            return true;

        if (root.TryGetProperty("EvidenceChunkCount", out var chunks)
            && chunks.ValueKind == JsonValueKind.Number
            && chunks.TryGetInt32(out var count)
            && count > 0)
        {
            return true;
        }

        if (!root.TryGetProperty("OperationConfigs", out var operations) || operations.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var operation in operations.EnumerateArray())
        {
            if (operation.ValueKind != JsonValueKind.Object)
                continue;

            if (HasItems(operation, "Conditions")
                || HasItems(operation, "CodeSystemMaps")
                || HasItems(operation, "ExtensionUrls")
                || HasText(operation, "SourceFhirPath")
                || HasText(operation, "TargetFhirPath")
                || HasText(operation, "ConditionTargetFhirPath")
                || HasText(operation, "ConditionTargetValue")
                || HasText(operation, "CodeMapFhirPath"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasItems(JsonElement item, string name)
        => item.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.Array
           && value.GetArrayLength() > 0;

    private static bool HasText(JsonElement item, string name)
        => item.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.String
           && !string.IsNullOrEmpty(value.GetString());
}

/// <summary>
/// Org-location totals for the run export warning. The mapping rows stay in Data Acquisition.
/// </summary>
public sealed class OrgLocationSummary
{
    public int ConfigurationCount { get; set; }
    public int ActiveConfigurationCount { get; set; }
    public int LocationMappingCount { get; set; }
    public int OrgLocationMappingCount { get; set; }
    public int EncounterMappingCount { get; set; }
    public int MappedToOrgCount { get; set; }
}
