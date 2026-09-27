using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace LantanaGroup.Link.Automation.Link.Validation;

/// <summary>
/// Detects Data Acquisition completing with <c>acquired=0</c> for a resource type
/// the Generation Manifest expected. That is an acquisition / FHIR-readiness
/// failure, not generated-data variance.
/// </summary>
public static class AcquisitionEmptyResultDetector
{
    private static readonly HashSet<string> PipelineDerivedTypes =
        new(StringComparer.OrdinalIgnoreCase) { "MeasureReport", "OperationOutcome", "Organization" };

    public sealed record EmptyAcquisition(
        string PatientId,
        string ResourceType,
        int ExpectedCount,
        int ActualCount);

    public static IReadOnlyList<EmptyAcquisition> Find(
        GenerationManifest manifest,
        IEnumerable<string> acquiredResourceIds,
        IReadOnlyList<PipelineDataReader.AcquisitionLogInfo>? logs = null)
    {
        var acquiredIds = acquiredResourceIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && id.Contains('/'))
            .ToList();

        var notReportablePatients = BuildNotReportablePatients(logs);
        var findings = new List<EmptyAcquisition>();

        foreach (var patientId in manifest.ExpectedSubmittedPatientIds())
        {
            if (string.IsNullOrWhiteSpace(patientId))
                continue;

            if (notReportablePatients.Contains(patientId))
                continue;

            var expectedCounts = ExpectedParameterQueryCounts(manifest, patientId);
            foreach (var (resourceType, expectedCount) in expectedCounts)
            {
                if (expectedCount <= 0)
                    continue;

                var actualCount = CountAcquiredForPatient(acquiredIds, patientId, resourceType);
                if (actualCount == 0)
                    findings.Add(new EmptyAcquisition(patientId, resourceType, expectedCount, actualCount));
            }
        }

        return findings;
    }

    public static bool ResourceIdBelongsToPatient(string resourceId, string patientId)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || string.IsNullOrWhiteSpace(patientId))
            return false;

        var slash = resourceId.IndexOf('/');
        var idPart = slash >= 0 ? resourceId[(slash + 1)..] : resourceId;
        if (string.IsNullOrWhiteSpace(idPart))
            return false;

        if (string.Equals(idPart, patientId, StringComparison.OrdinalIgnoreCase))
            return true;

        return idPart.StartsWith(patientId + "-", StringComparison.OrdinalIgnoreCase);
    }

    internal static Dictionary<string, int> ExpectedParameterQueryCounts(GenerationManifest manifest, string patientId)
    {
        var typesToCheck = manifest.ParameterQueryResourceTypes is { Count: > 0 }
            ? manifest.ParameterQueryResourceTypes
            : manifest.AcquiredResourceTypes;

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (manifest.SimulatedAcquiredResourceKeysByPatient.TryGetValue(patientId, out var simulated)
            && simulated.Count > 0)
        {
            AddCountsFromKeys(counts, simulated);
        }
        else if (manifest.ResourceKeysByPatient.TryGetValue(patientId, out var generated)
                 && generated.Count > 0)
        {
            AddCountsFromKeys(counts, generated);
        }
        else
        {
            var absCounts = manifest.GetExpectedAbsCountsForPatient(patientId);
            if (absCounts != null)
            {
                foreach (var (type, count) in absCounts)
                    counts[type] = count;
            }
        }

        foreach (var derived in PipelineDerivedTypes)
            counts.Remove(derived);
        counts.Remove("Patient");

        if (typesToCheck is { Count: > 0 })
        {
            var filtered = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (type, count) in counts)
            {
                if (typesToCheck.Contains(type))
                    filtered[type] = count;
            }

            return filtered;
        }

        return counts;
    }

    private static void AddCountsFromKeys(Dictionary<string, int> counts, IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            var slash = key.IndexOf('/');
            var type = slash > 0 ? key[..slash] : key;
            if (string.IsNullOrWhiteSpace(type))
                continue;

            counts[type] = counts.TryGetValue(type, out var current) ? current + 1 : 1;
        }
    }

    private static int CountAcquiredForPatient(
        IReadOnlyList<string> acquiredResourceIds,
        string patientId,
        string resourceType)
    {
        var prefix = resourceType + "/";
        var count = 0;
        foreach (var id in acquiredResourceIds)
        {
            if (!id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (ResourceIdBelongsToPatient(id, patientId))
                count++;
        }

        return count;
    }

    private static HashSet<string> BuildNotReportablePatients(
        IReadOnlyList<PipelineDataReader.AcquisitionLogInfo>? logs)
    {
        var notReportable = new HashSet<string>(StringComparer.Ordinal);
        if (logs == null || logs.Count == 0)
            return notReportable;

        foreach (var group in logs
                     .Where(l => !string.IsNullOrWhiteSpace(l.PatientId))
                     .GroupBy(l => l.PatientId!, StringComparer.Ordinal))
        {
            var hasCompleted = group.Any(l =>
                string.Equals(l.Status, "Completed", StringComparison.OrdinalIgnoreCase));
            var allNotReportable = group.All(l =>
                string.Equals(l.Status, "NotReportable", StringComparison.OrdinalIgnoreCase));

            // Patients who never became reportable skip supplemental parameter queries
            // (Observation, Condition, ...). Empty acquisition for those types is expected.
            if (allNotReportable || !hasCompleted)
                notReportable.Add(group.Key);
        }

        return notReportable;
    }
}
