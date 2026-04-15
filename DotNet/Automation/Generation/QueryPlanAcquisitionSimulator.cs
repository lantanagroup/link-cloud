using System.Globalization;
using System.Text.Json;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Deterministically simulates Data Acquisition results from generated bundles + query plan,
/// returning acquired resource keys (Type/Id) by patient.
/// </summary>
public static class QueryPlanAcquisitionSimulator
{
    private sealed record GeneratedResource(string PatientId, string ResourceType, string ResourceId, string Key, JsonElement Resource);

    public static IReadOnlyDictionary<string, HashSet<string>> SimulateAcquiredKeysByPatient(
        IReadOnlyList<string> patientIds,
        IReadOnlyList<(string Name, string Json)> bundles,
        QueryPlanInput queryPlan,
        string? reportStart = null,
        string? reportEnd = null)
    {
        var byPatient = BuildGeneratedResourceIndex(patientIds, bundles);

        var hasStart = DateTimeOffset.TryParse(reportStart, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start);
        var hasEnd = DateTimeOffset.TryParse(reportEnd, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var end);

        var results = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var patientId in patientIds)
        {
            var acquired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var acquiredByType = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var query in queryPlan.InitialQueries.Concat(queryPlan.SupplementalQueries))
            {
                if (string.IsNullOrWhiteSpace(query.ResourceType))
                    continue;

                if (query.IsParameterQuery)
                {
                    if (!byPatient.TryGetValue(patientId, out var patientResources)
                        || !patientResources.TryGetValue(query.ResourceType, out var candidates))
                    {
                        continue;
                    }

                    foreach (var resource in candidates)
                    {
                        if (!MatchesParameterQuery(resource, query, hasStart ? start : null, hasEnd ? end : null, acquiredByType))
                            continue;

                        acquired.Add(resource.Key);
                        AddByType(acquiredByType, resource.ResourceType, resource.ResourceId);
                    }
                }
                else if (query.IsReferenceQuery)
                {
                    if (!byPatient.TryGetValue(patientId, out var patientResources)
                        || !patientResources.TryGetValue(query.ResourceType, out var candidates)
                        || candidates.Count == 0)
                    {
                        continue;
                    }

                    var referencedIds = CollectReferencedIds(acquiredByType, query.ResourceType, byPatient, patientId);
                    if (referencedIds.Count == 0)
                        continue;

                    foreach (var resource in candidates)
                    {
                        if (!referencedIds.Contains(resource.ResourceId))
                            continue;

                        acquired.Add(resource.Key);
                        AddByType(acquiredByType, resource.ResourceType, resource.ResourceId);
                    }
                }
            }

            results[patientId] = acquired;
        }

        return results;
    }

    /// <summary>
    /// Simulates acquired resource keys for a single patient from pre-parsed resource entries.
    /// Used by the pipeline to avoid retaining serialized bundle JSON across all patients.
    /// </summary>
    /// <param name="patientId">The patient ID being simulated.</param>
    /// <param name="patientResourceEntries">
    /// Pre-parsed resource entries as (ResourceType, ResourceId, Key, JsonElement) tuples.
    /// These are built from the patient's in-memory FHIR entries before JSON is discarded.
    /// </param>
    /// <param name="sharedResourceEntries">
    /// Shared infrastructure resources (Location, Medication, Device, etc.) that may be
    /// referenced by the patient's resources.
    /// </param>
    /// <param name="queryPlan">The query plan to simulate.</param>
    /// <param name="reportStart">Optional report period start date.</param>
    /// <param name="reportEnd">Optional report period end date.</param>
    public static HashSet<string> SimulateAcquiredKeysForPatient(
        string patientId,
        IReadOnlyList<(string ResourceType, string ResourceId, string Key, JsonElement Resource)> patientResourceEntries,
        IReadOnlyList<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>? sharedResourceEntries,
        QueryPlanInput queryPlan,
        string? reportStart = null,
        string? reportEnd = null)
    {
        var hasStart = DateTimeOffset.TryParse(reportStart, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start);
        var hasEnd = DateTimeOffset.TryParse(reportEnd, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var end);

        // Build per-type index for this patient (patient resources + shared)
        var typeMap = new Dictionary<string, List<GeneratedResource>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (resourceType, resourceId, key, resource) in patientResourceEntries)
        {
            if (!typeMap.TryGetValue(resourceType, out var list))
            {
                list = [];
                typeMap[resourceType] = list;
            }
            list.Add(new GeneratedResource(patientId, resourceType, resourceId, key, resource));
        }

        if (sharedResourceEntries != null)
        {
            foreach (var (resourceType, resourceId, key, resource) in sharedResourceEntries)
            {
                if (!typeMap.TryGetValue(resourceType, out var list))
                {
                    list = [];
                    typeMap[resourceType] = list;
                }
                list.Add(new GeneratedResource(patientId, resourceType, resourceId, key, resource));
            }
        }

        // Wrap in the by-patient structure the private helpers expect
        var byPatient = new Dictionary<string, Dictionary<string, List<GeneratedResource>>>(StringComparer.Ordinal)
        {
            [patientId] = typeMap
        };

        var acquired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var acquiredByType = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var query in queryPlan.InitialQueries.Concat(queryPlan.SupplementalQueries))
        {
            if (string.IsNullOrWhiteSpace(query.ResourceType))
                continue;

            if (query.IsParameterQuery)
            {
                if (!typeMap.TryGetValue(query.ResourceType, out var candidates))
                    continue;

                foreach (var resource in candidates)
                {
                    if (!MatchesParameterQuery(resource, query, hasStart ? start : null, hasEnd ? end : null, acquiredByType))
                        continue;

                    acquired.Add(resource.Key);
                    AddByType(acquiredByType, resource.ResourceType, resource.ResourceId);
                }
            }
            else if (query.IsReferenceQuery)
            {
                if (!typeMap.TryGetValue(query.ResourceType, out var candidates) || candidates.Count == 0)
                    continue;

                var referencedIds = CollectReferencedIds(acquiredByType, query.ResourceType, byPatient, patientId);
                if (referencedIds.Count == 0)
                    continue;

                foreach (var resource in candidates)
                {
                    if (!referencedIds.Contains(resource.ResourceId))
                        continue;

                    acquired.Add(resource.Key);
                    AddByType(acquiredByType, resource.ResourceType, resource.ResourceId);
                }
            }
        }

        return acquired;
    }

    private static Dictionary<string, Dictionary<string, List<GeneratedResource>>> BuildGeneratedResourceIndex(
        IReadOnlyList<string> patientIds,
        IReadOnlyList<(string Name, string Json)> bundles)
    {
        var patientSet = patientIds.ToHashSet(StringComparer.Ordinal);
        var index = new Dictionary<string, Dictionary<string, List<GeneratedResource>>>(StringComparer.Ordinal);

        foreach (var (_, json) in bundles)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("resource", out var resource) || resource.ValueKind != JsonValueKind.Object)
                    continue;
                if (!entry.TryGetProperty("request", out var request) || request.ValueKind != JsonValueKind.Object)
                    continue;
                if (!request.TryGetProperty("url", out var urlProp) || urlProp.ValueKind != JsonValueKind.String)
                    continue;

                var key = urlProp.GetString();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var slash = key.IndexOf('/');
                if (slash <= 0 || slash >= key.Length - 1)
                    continue;

                var resourceType = key[..slash];
                var resourceId = key[(slash + 1)..];

                var ownerPatientId = string.Empty;
                foreach (var pid in patientSet)
                {
                    if (resourceId.StartsWith(pid, StringComparison.Ordinal))
                    {
                        ownerPatientId = pid;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(ownerPatientId))
                    continue;

                if (!index.TryGetValue(ownerPatientId, out var typeMap))
                {
                    typeMap = new Dictionary<string, List<GeneratedResource>>(StringComparer.OrdinalIgnoreCase);
                    index[ownerPatientId] = typeMap;
                }

                if (!typeMap.TryGetValue(resourceType, out var list))
                {
                    list = [];
                    typeMap[resourceType] = list;
                }

                list.Add(new GeneratedResource(ownerPatientId, resourceType, resourceId, key, resource.Clone()));
            }
        }

        return index;
    }

    private static bool MatchesParameterQuery(
        GeneratedResource resource,
        QueryPlanQueryEntry query,
        DateTimeOffset? periodStart,
        DateTimeOffset? periodEnd,
        Dictionary<string, HashSet<string>> acquiredByType)
    {
        foreach (var p in query.Parameters)
        {
            if (string.Equals(p.ParameterType, "ResourceIds", StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.Name, "encounter", StringComparison.OrdinalIgnoreCase))
            {
                if (!acquiredByType.TryGetValue("Encounter", out var encounterIds) || encounterIds.Count == 0)
                    return false;

                if (!TryGetReferencedResourceId(resource.Resource, "Encounter", out var encounterId)
                    || !encounterIds.Contains(encounterId))
                {
                    return false;
                }
            }

            if (string.Equals(p.ParameterType, "Literal", StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.Name, "category", StringComparison.OrdinalIgnoreCase))
            {
                if (!MatchesLiteralCategory(resource.Resource, p.Literal))
                    return false;
            }

            if (string.Equals(p.Name, "date", StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.ParameterType, "Variable", StringComparison.OrdinalIgnoreCase)
                && TryGetResourceDate(resource.ResourceType, resource.Resource, out var resourceDate))
            {
                if (p.Format?.StartsWith("ge", StringComparison.OrdinalIgnoreCase) == true
                    && periodStart.HasValue
                    && resourceDate < periodStart.Value)
                {
                    return false;
                }

                if (p.Format?.StartsWith("le", StringComparison.OrdinalIgnoreCase) == true
                    && periodEnd.HasValue
                    && resourceDate > periodEnd.Value)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static HashSet<string> CollectReferencedIds(
        Dictionary<string, HashSet<string>> acquiredByType,
        string targetType,
        Dictionary<string, Dictionary<string, List<GeneratedResource>>> byPatient,
        string patientId)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!byPatient.TryGetValue(patientId, out var typeMap))
            return result;

        foreach (var (resourceType, ids) in acquiredByType)
        {
            if (!typeMap.TryGetValue(resourceType, out var resources))
                continue;

            foreach (var resource in resources)
            {
                if (!ids.Contains(resource.ResourceId))
                    continue;

                foreach (var reference in EnumerateReferences(resource.Resource))
                {
                    if (!reference.StartsWith(targetType + "/", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var id = reference[(targetType.Length + 1)..];
                    if (!string.IsNullOrWhiteSpace(id))
                        result.Add(id);
                }
            }
        }

        return result;
    }

    private static IEnumerable<string> EnumerateReferences(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "reference", StringComparison.Ordinal)
                        && prop.Value.ValueKind == JsonValueKind.String
                        && prop.Value.GetString() is { Length: > 0 } reference)
                    {
                        yield return reference;
                    }

                    foreach (var child in EnumerateReferences(prop.Value))
                        yield return child;
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var child in EnumerateReferences(item))
                        yield return child;
                }
                break;
        }
    }

    private static bool TryGetReferencedResourceId(JsonElement resource, string resourceType, out string id)
    {
        foreach (var reference in EnumerateReferences(resource))
        {
            if (!reference.StartsWith(resourceType + "/", StringComparison.OrdinalIgnoreCase))
                continue;

            id = reference[(resourceType.Length + 1)..];
            return !string.IsNullOrWhiteSpace(id);
        }

        id = string.Empty;
        return false;
    }

    private static bool MatchesLiteralCategory(JsonElement resource, string? literal)
    {
        if (string.IsNullOrWhiteSpace(literal))
            return true;

        var accepted = literal.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!resource.TryGetProperty("category", out var categories) || categories.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var category in categories.EnumerateArray())
        {
            if (!category.TryGetProperty("coding", out var codingArray) || codingArray.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var coding in codingArray.EnumerateArray())
            {
                if (!coding.TryGetProperty("code", out var codeProp) || codeProp.ValueKind != JsonValueKind.String)
                    continue;

                var code = codeProp.GetString();
                if (!string.IsNullOrWhiteSpace(code) && accepted.Contains(code))
                    return true;
            }
        }

        return false;
    }

    private static bool TryGetResourceDate(string resourceType, JsonElement resource, out DateTimeOffset date)
    {
        static bool TryParseStringProperty(JsonElement element, string propertyName, out DateTimeOffset parsed)
        {
            if (element.TryGetProperty(propertyName, out var prop)
                && prop.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(prop.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed))
            {
                return true;
            }

            parsed = default;
            return false;
        }

        static bool TryParseNestedStringProperty(JsonElement element, string parent, string child, out DateTimeOffset parsed)
        {
            if (element.TryGetProperty(parent, out var parentProp)
                && parentProp.ValueKind == JsonValueKind.Object
                && TryParseStringProperty(parentProp, child, out parsed))
            {
                return true;
            }

            parsed = default;
            return false;
        }

        return resourceType switch
        {
            "Encounter" => TryParseNestedStringProperty(resource, "period", "start", out date),
            "Condition" => TryParseStringProperty(resource, "recordedDate", out date) || TryParseStringProperty(resource, "onsetDateTime", out date),
            "Procedure" => TryParseStringProperty(resource, "performedDateTime", out date) || TryParseNestedStringProperty(resource, "performedPeriod", "start", out date),
            "Observation" => TryParseStringProperty(resource, "effectiveDateTime", out date) || TryParseStringProperty(resource, "issued", out date),
            "DiagnosticReport" => TryParseStringProperty(resource, "effectiveDateTime", out date) || TryParseStringProperty(resource, "issued", out date),
            "ServiceRequest" => TryParseStringProperty(resource, "authoredOn", out date),
            "MedicationRequest" => TryParseStringProperty(resource, "authoredOn", out date),
            _ => TryParseStringProperty(resource, "authoredOn", out date)
                 || TryParseStringProperty(resource, "issued", out date)
                 || TryParseStringProperty(resource, "effectiveDateTime", out date)
                 || TryParseStringProperty(resource, "recordedDate", out date)
        };
    }

    private static void AddByType(Dictionary<string, HashSet<string>> acquiredByType, string resourceType, string resourceId)
    {
        if (!acquiredByType.TryGetValue(resourceType, out var ids))
        {
            ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            acquiredByType[resourceType] = ids;
        }

        ids.Add(resourceId);
    }
}
