using System.Text.Json;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Lightweight snapshot of the actual ABS (Azure Blob Storage) upload contents,
/// captured after report download during a test run.
/// Used by the Manifest detail page to show a Generated vs Actual comparison.
/// </summary>
public sealed class AbsUploadSnapshot
{
    /// <summary>Total resource count across all patient artifacts.</summary>
    public int TotalResourceCount { get; init; }

    /// <summary>Patient IDs that had artifacts in the upload.</summary>
    public IReadOnlyList<string> PatientIds { get; init; } = [];

    /// <summary>Aggregate resource type ? count across all patient artifacts.</summary>
    public IReadOnlyDictionary<string, int> TotalCountsByType { get; init; } = new Dictionary<string, int>();

    /// <summary>Per-patient resource type ? count.</summary>
    public IReadOnlyDictionary<string, Dictionary<string, int>> ResourceCountsByPatient { get; init; }
        = new Dictionary<string, Dictionary<string, int>>();

    /// <summary>Resource types found in the manifest.ndjson (Organization, Device, List, MeasureReport).</summary>
    public IReadOnlyList<string> ManifestResourceTypes { get; init; } = [];

    /// <summary>Number of resources in manifest.ndjson.</summary>
    public int ManifestResourceCount { get; init; }

    /// <summary>
    /// Builds an <see cref="AbsUploadSnapshot"/> from the downloaded internal ABS resources.
    /// </summary>
    public static AbsUploadSnapshot Build(IDictionary<string, object> internalAbsResources)
    {
        var patientIds = new List<string>();
        var totalsByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var countsByPatient = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var totalCount = 0;

        // Parse manifest.ndjson for manifest-level stats
        var manifestTypes = new List<string>();
        var manifestCount = 0;
        if (internalAbsResources.TryGetValue("manifest.ndjson", out var manifestObj) && manifestObj is string manifestNdjson)
        {
            foreach (var line in manifestNdjson.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                using var doc = JsonDocument.Parse(line);
                var rt = doc.RootElement.TryGetProperty("resourceType", out var rtProp)
                    ? rtProp.GetString() : null;
                if (!string.IsNullOrWhiteSpace(rt))
                {
                    manifestCount++;
                    if (!manifestTypes.Contains(rt))
                        manifestTypes.Add(rt);
                }
            }
        }

        // Parse patient-*.ndjson files
        foreach (var (key, value) in internalAbsResources)
        {
            if (!key.StartsWith("patient-", StringComparison.OrdinalIgnoreCase) ||
                !key.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase) ||
                value is not string ndjson)
                continue;

            // Extract patient ID from filename: patient-{id}.ndjson
            var patientId = key["patient-".Length..^".ndjson".Length];
            patientIds.Add(patientId);

            var patientCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in ndjson.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                using var doc = JsonDocument.Parse(line);
                var rt = doc.RootElement.TryGetProperty("resourceType", out var rtProp)
                    ? rtProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(rt)) continue;

                totalCount++;
                patientCounts[rt] = patientCounts.TryGetValue(rt, out var c) ? c + 1 : 1;
                totalsByType[rt] = totalsByType.TryGetValue(rt, out var tc) ? tc + 1 : 1;
            }

            countsByPatient[patientId] = patientCounts;
        }

        patientIds.Sort(StringComparer.OrdinalIgnoreCase);

        return new AbsUploadSnapshot
        {
            TotalResourceCount = totalCount,
            PatientIds = patientIds,
            TotalCountsByType = totalsByType,
            ResourceCountsByPatient = countsByPatient,
            ManifestResourceTypes = manifestTypes,
            ManifestResourceCount = manifestCount
        };
    }
}
