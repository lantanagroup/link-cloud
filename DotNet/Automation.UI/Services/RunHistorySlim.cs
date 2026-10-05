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
            FhirQueries = [],
            ResourceTypes = ResourceTypes(log)
        }).ToList();
    }

    private static List<string> ResourceTypes(PipelineDataReader.AcquisitionLogInfo log)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var types = new List<string>();
        Add(log.ResourceTypes);
        if (log.FhirQueries != null)
        {
            foreach (var query in log.FhirQueries)
                Add(query?.ResourceTypes);
        }

        return types;

        void Add(IEnumerable<string>? values)
        {
            if (values == null)
                return;

            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                    continue;

                types.Add(value);
            }
        }
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
            StepsCollapsed = false,
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
}

public sealed class OrgLocationSummary
{
    public int ConfigurationCount { get; set; }
    public int ActiveConfigurationCount { get; set; }
    public int LocationMappingCount { get; set; }
    public int OrgLocationMappingCount { get; set; }
    public int EncounterMappingCount { get; set; }
    public int MappedToOrgCount { get; set; }
}
