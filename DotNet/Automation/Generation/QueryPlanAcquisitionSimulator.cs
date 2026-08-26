using LantanaGroup.Automation.Helpers;
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
    /// <param name="clinicalPeriodStart">Optional clinical period start date (ISO-8601). When provided, parameter queries with a <c>date=ge…</c> filter exclude resources whose date range falls entirely before this value (FHIR overlap semantics).</param>
    /// <param name="clinicalPeriodEnd">Optional clinical period end date (ISO-8601). When provided, parameter queries with a <c>date=le…</c> filter exclude resources whose date range falls entirely after this value (FHIR overlap semantics).</param>
    /// <param name="output">
    /// Optional diagnostic sink. When non-null, the simulator emits a single warning per
    /// (resource-key) when a Parameter query has a <c>date</c> filter but the resource's
    /// shape does not carry a recognized date field. Such resources are <b>excluded</b> from
    /// the predicted-acquired set (fail-closed) so that prediction stays honest in the face
    /// of unfamiliar imported FHIR shapes.
    /// </param>
    public static HashSet<string> SimulateAcquiredKeysForPatient(
        string patientId,
        IReadOnlyList<(string ResourceType, string ResourceId, string Key, JsonElement Resource)> patientResourceEntries,
        IReadOnlyList<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>? sharedResourceEntries,
        QueryPlanInput queryPlan,
        string? clinicalPeriodStart = null,
        string? clinicalPeriodEnd = null,
        IAutomationOutput? output = null,
        bool allowEncounterAnchoredDateOverrideForOutOfRange = true)
    {
        var hasStart = DateTimeOffset.TryParse(clinicalPeriodStart, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start);
        var hasEnd = DateTimeOffset.TryParse(clinicalPeriodEnd, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var end);

        // DataAcquisition variable substitution normalizes PeriodStart/PeriodEnd to day bounds
        // (00:00:00Z / 23:59:59Z) before applying ge/le formatting. Mirror that behavior so
        // simulator date filtering matches runtime acquisition semantics.
        if (hasStart)
            start = new DateTimeOffset(start.UtcDateTime.Date, TimeSpan.Zero);
        if (hasEnd)
            end = new DateTimeOffset(end.UtcDateTime.Date.AddDays(1).AddSeconds(-1), TimeSpan.Zero);

        // Track resource keys we've already emitted a "no recognized date" warning for, so
        // a single missing-date resource doesn't spam the log once per date-parameter iteration.
        var warnedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                    if (!MatchesParameterQuery(
                            resource,
                            query,
                            hasStart ? start : null,
                            hasEnd ? end : null,
                            acquiredByType,
                            warnedKeys,
                            output,
                            allowEncounterAnchoredDateOverrideForOutOfRange))
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

    private static bool MatchesParameterQuery(
        GeneratedResource resource,
        QueryPlanQueryEntry query,
        DateTimeOffset? periodStart,
        DateTimeOffset? periodEnd,
        Dictionary<string, HashSet<string>> acquiredByType,
        HashSet<string> warnedKeys,
        IAutomationOutput? output,
        bool allowEncounterAnchoredDateOverrideForOutOfRange)
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

            if (string.Equals(p.ParameterType, "Literal", StringComparison.OrdinalIgnoreCase))
            {
                // Skip FHIR pagination/control parameters — they are not filters.
                if (string.Equals(p.Name, "_count", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Name, "_sort", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Name, "_include", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Name, "_revinclude", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!MatchesLiteralFilter(resource.Resource, p.Name, p.Literal))
                    return false;
            }

            if (IsTemporalSearchParam(p.Name)
                && string.Equals(p.ParameterType, "Variable", StringComparison.OrdinalIgnoreCase))
            {
                var isGe = p.Format?.StartsWith("ge", StringComparison.OrdinalIgnoreCase) == true;
                var isLe = p.Format?.StartsWith("le", StringComparison.OrdinalIgnoreCase) == true;

                // Pick the bound this specific parameter enforces. When it's null the filter
                // is vacuous (e.g. caller didn't supply a clinical period) — skip silently
                // so unrecognized date shapes don't get penalized for a non-existent rule.
                var bound = isGe ? periodStart : isLe ? periodEnd : null;
                if (bound == null)
                    continue;

                // Observation/DiagnosticReport `date` is FHIR effective[x] only.
                // Including issued here over-predicts boundary resources (issued slightly
                // after period start while effective is before it) — the DxRpt-042 class of
                // scheduled ABS misses.
                if (string.Equals(p.Name, "date", StringComparison.OrdinalIgnoreCase)
                    && (string.Equals(resource.ResourceType, "Observation", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(resource.ResourceType, "DiagnosticReport", StringComparison.OrdinalIgnoreCase)))
                {
                    var matchedAny = false;
                    var recognizedAny = false;

                    if (TryGetEffective(resource.Resource, out var effStart, out var effEnd))
                    {
                        recognizedAny = true;
                        matchedAny |= isGe ? effEnd >= bound.Value : effStart <= bound.Value;
                    }

                    if (!recognizedAny)
                    {
                        if (HasEncounterAnchoredDateOverride(resource, acquiredByType))
                            continue;

                        if (output != null && warnedKeys.Add(resource.Key))
                        {
                            output.WriteLine(
                                $"  [simulator] WARNING: {resource.Key} has no recognized date field for the " +
                                $"'{resource.ResourceType}' Parameter query '{p.Name}' filter; excluding from " +
                                "predicted-acquired set (fail-closed). Extend TryGetResourceDateRangeForParam to model this shape.");
                        }
                        return false;
                    }

                    if (!matchedAny)
                    {
                        if (allowEncounterAnchoredDateOverrideForOutOfRange
                            && HasEncounterAnchoredDateOverride(resource, acquiredByType))
                        {
                            continue;
                        }

                        return false;
                    }

                    continue;
                }

                // Only one bound is being checked per parameter. We need both ends of the
                // resource's date range so we can compute correct FHIR overlap semantics:
                //   ge{S}:  resource.End   >= S   (resource extends into [S, +inf))
                //   le{E}:  resource.Start <= E   (resource extends into (-inf, E])
                // For instant types (e.g. authoredOn) start == end.
                if (!TryGetResourceDateRangeForParam(p.Name, resource.ResourceType, resource.Resource,
                        out var resourceStart, out var resourceEnd))
                {
                    if (string.Equals(p.Name, "date", StringComparison.OrdinalIgnoreCase)
                        && HasEncounterAnchoredDateOverride(resource, acquiredByType))
                    {
                        continue;
                    }

                    // Fail closed: the query has a date filter and we don't recognize the
                    // resource's date shape, so we can't honestly say it falls in-period.
                    // Drop it and warn once per resource key so unfamiliar shapes surface.
                    if (output != null && warnedKeys.Add(resource.Key))
                    {
                        output.WriteLine(
                            $"  [simulator] WARNING: {resource.Key} has no recognized date field for the " +
                            $"'{resource.ResourceType}' Parameter query '{p.Name}' filter; excluding from " +
                            "predicted-acquired set (fail-closed). Extend TryGetResourceDateRangeForParam to model this shape.");
                    }
                    return false;
                }

                if (isGe && resourceEnd < bound.Value)
                {
                    if (allowEncounterAnchoredDateOverrideForOutOfRange
                        && string.Equals(p.Name, "date", StringComparison.OrdinalIgnoreCase)
                        && HasEncounterAnchoredDateOverride(resource, acquiredByType))
                    {
                        continue;
                    }

                    return false;
                }

                if (isLe && resourceStart > bound.Value)
                {
                    if (allowEncounterAnchoredDateOverrideForOutOfRange
                        && string.Equals(p.Name, "date", StringComparison.OrdinalIgnoreCase)
                        && HasEncounterAnchoredDateOverride(resource, acquiredByType))
                    {
                        continue;
                    }

                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Some DA query paths are encounter-anchored and can include resources linked to an
    /// already-acquired encounter. The simulator always uses this as a fallback when a
    /// resource doesn't expose a recognizable date field, and can optionally apply it to
    /// out-of-range recognized-date cases when enabled by the caller.
    ///
    /// This override is restricted to date-filtered Observation/DiagnosticReport/Procedure
    /// resources and only when their Encounter reference resolves to an acquired Encounter id.
    /// </summary>
    private static bool HasEncounterAnchoredDateOverride(
        GeneratedResource resource,
        Dictionary<string, HashSet<string>> acquiredByType)
    {
        // DiagnosticReport date search is effective[x] only. Encounter-anchoring DRs
        // (including unrecognized-date fallback) over-predicts scheduled ABS
        // (DxRpt-042 / DxRpt-043 class).
        if (!(string.Equals(resource.ResourceType, "Observation", StringComparison.OrdinalIgnoreCase)
              || string.Equals(resource.ResourceType, "Procedure", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!acquiredByType.TryGetValue("Encounter", out var encounterIds) || encounterIds.Count == 0)
            return false;

        return TryGetReferencedResourceId(resource.Resource, "Encounter", out var encounterId)
               && encounterIds.Contains(encounterId);
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

        if (string.Equals(targetType, "Location", StringComparison.OrdinalIgnoreCase))
            ExpandReferencedLocationParents(result, typeMap);

        return result;
    }

    /// <summary>
    /// Data Acquisition follows <c>Location.partOf</c> when resolving referenced locations
    /// (ward → hospital). Expand the referenced-id set so the Hospital parent is acquired
    /// along with encounter stay locations.
    /// </summary>
    private static void ExpandReferencedLocationParents(
        HashSet<string> referencedIds,
        Dictionary<string, List<GeneratedResource>> typeMap)
    {
        if (!typeMap.TryGetValue("Location", out var locations) || locations.Count == 0)
            return;

        var byId = new Dictionary<string, GeneratedResource>(StringComparer.OrdinalIgnoreCase);
        foreach (var location in locations)
        {
            if (!string.IsNullOrWhiteSpace(location.ResourceId))
                byId[location.ResourceId] = location;
        }

        var pending = new Queue<string>(referencedIds);
        while (pending.Count > 0)
        {
            var id = pending.Dequeue();
            if (!byId.TryGetValue(id, out var location))
                continue;

            foreach (var reference in EnumerateReferences(location.Resource))
            {
                if (!reference.StartsWith("Location/", StringComparison.OrdinalIgnoreCase))
                    continue;

                var parentId = reference["Location/".Length..];
                if (string.IsNullOrWhiteSpace(parentId))
                    continue;
                if (referencedIds.Add(parentId))
                    pending.Enqueue(parentId);
            }
        }
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

    /// <summary>
    /// Generic literal filter. Handles:
    /// - CodeableConcept arrays (e.g. <c>category</c>) — matches if any coding.code is in the accepted set.
    /// - Simple code/string fields (e.g. <c>intent</c>, <c>status</c>) — matches if the field value is in the accepted set.
    /// </summary>
    private static bool MatchesLiteralFilter(JsonElement resource, string paramName, string? literal)
    {
        if (string.IsNullOrWhiteSpace(literal))
            return true;

        var accepted = literal.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!resource.TryGetProperty(paramName, out var fieldValue))
            return false;

        // Case 1: Simple string/code field (e.g. intent, status)
        if (fieldValue.ValueKind == JsonValueKind.String)
        {
            var value = fieldValue.GetString();
            return !string.IsNullOrWhiteSpace(value) && accepted.Contains(value);
        }

        // Case 2: CodeableConcept array (e.g. category)
        if (fieldValue.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in fieldValue.EnumerateArray())
            {
                if (item.TryGetProperty("coding", out var codingArray) && codingArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var coding in codingArray.EnumerateArray())
                    {
                        if (coding.TryGetProperty("code", out var codeProp)
                            && codeProp.ValueKind == JsonValueKind.String)
                        {
                            var code = codeProp.GetString();
                            if (!string.IsNullOrWhiteSpace(code) && accepted.Contains(code))
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        // Case 3: Single CodeableConcept object (e.g. code)
        if (fieldValue.ValueKind == JsonValueKind.Object
            && fieldValue.TryGetProperty("coding", out var singleCodingArray)
            && singleCodingArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var coding in singleCodingArray.EnumerateArray())
            {
                if (coding.TryGetProperty("code", out var codeProp)
                    && codeProp.ValueKind == JsonValueKind.String)
                {
                    var code = codeProp.GetString();
                    if (!string.IsNullOrWhiteSpace(code) && accepted.Contains(code))
                        return true;
                }
            }
            return false;
        }

        return false;
    }

    /// <summary>
    /// Determines whether a FHIR search parameter name is a temporal (date-like) filter.
    /// Recognized names: <c>date</c>, <c>authoredon</c>, <c>authored</c>, <c>issued</c>,
    /// <c>effective</c>, <c>effective-time</c>, <c>onset-date</c>, <c>recorded-date</c>.
    /// </summary>
    private static bool IsTemporalSearchParam(string paramName)
        => string.Equals(paramName, "date", StringComparison.OrdinalIgnoreCase)
           || string.Equals(paramName, "authoredon", StringComparison.OrdinalIgnoreCase)
           || string.Equals(paramName, "authored", StringComparison.OrdinalIgnoreCase)
           || string.Equals(paramName, "issued", StringComparison.OrdinalIgnoreCase)
           || string.Equals(paramName, "effective", StringComparison.OrdinalIgnoreCase)
           || string.Equals(paramName, "effective-time", StringComparison.OrdinalIgnoreCase)
           || string.Equals(paramName, "onset-date", StringComparison.OrdinalIgnoreCase)
           || string.Equals(paramName, "recorded-date", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves the resource's date range for a given FHIR search parameter name.
    /// Named search parameters like <c>authoredon</c> map to specific fields regardless of
    /// what <c>TryGetResourceDateRange</c> would pick for the generic <c>date</c> param.
    /// </summary>
    private static bool TryGetResourceDateRangeForParam(string paramName, string resourceType,
        JsonElement resource, out DateTimeOffset start, out DateTimeOffset end)
    {
        // Named search parameters that map to specific fields.
        if (string.Equals(paramName, "authoredon", StringComparison.OrdinalIgnoreCase)
            || string.Equals(paramName, "authored", StringComparison.OrdinalIgnoreCase))
        {
            return TryGetInstant(resource, "authoredOn", out start, out end);
        }

        if (string.Equals(paramName, "issued", StringComparison.OrdinalIgnoreCase))
        {
            return TryGetInstant(resource, "issued", out start, out end);
        }

        if (string.Equals(paramName, "effective", StringComparison.OrdinalIgnoreCase)
            || string.Equals(paramName, "effective-time", StringComparison.OrdinalIgnoreCase))
        {
            return TryGetEffective(resource, out start, out end);
        }

        if (string.Equals(paramName, "onset-date", StringComparison.OrdinalIgnoreCase))
        {
            return TryGetInstant(resource, "onsetDateTime", out start, out end)
                   || TryGetPeriod(resource, "onsetPeriod", out start, out end);
        }

        if (string.Equals(paramName, "recorded-date", StringComparison.OrdinalIgnoreCase))
        {
            return TryGetInstant(resource, "recordedDate", out start, out end);
        }

        // Generic "date" → resource-type-aware resolution.
        return TryGetResourceDateRange(resourceType, resource, out start, out end);
    }

    /// <summary>
    /// Returns the resource's date range (start, end) for FHIR <c>date</c> search-param
    /// matching with overlap semantics. For instant-style fields the start and end are
    /// equal. Returns <c>false</c> when the resource shape carries no recognized date —
    /// callers fail-closed and warn so unfamiliar imported shapes get caught early.
    ///
    /// Mappings track the FHIR R4 <c>date</c> search parameter for each resource type:
    /// <list type="bullet">
    ///   <item>Encounter — <c>period</c> (overlap)</item>
    ///   <item>Observation — <c>effectiveDateTime</c> | <c>effectivePeriod</c> | <c>effectiveInstant</c>, fall back to <c>issued</c></item>
    ///   <item>DiagnosticReport — <c>effectiveDateTime</c> | <c>effectivePeriod</c>, fall back to <c>issued</c></item>
    ///   <item>Procedure — <c>performedDateTime</c> | <c>performedPeriod</c></item>
    ///   <item>Condition — <c>recordedDate</c> | <c>onsetDateTime</c> | <c>onsetPeriod</c></item>
    ///   <item>ServiceRequest / MedicationRequest — <c>authoredOn</c> (instant)</item>
    /// </list>
    /// </summary>
    private static bool TryGetResourceDateRange(string resourceType, JsonElement resource,
        out DateTimeOffset start, out DateTimeOffset end)
    {
        start = default;
        end = default;

        switch (resourceType)
        {
            case "Encounter":
                return TryGetPeriod(resource, "period", out start, out end);

            case "Observation":
                return TryGetEffective(resource, out start, out end)
                    || TryGetInstant(resource, "issued", out start, out end);

            case "DiagnosticReport":
                return TryGetEffective(resource, out start, out end)
                    || TryGetInstant(resource, "issued", out start, out end);

            case "Procedure":
                return TryGetInstant(resource, "performedDateTime", out start, out end)
                    || TryGetPeriod(resource, "performedPeriod", out start, out end);

            case "Condition":
                return TryGetInstant(resource, "recordedDate", out start, out end)
                    || TryGetInstant(resource, "onsetDateTime", out start, out end)
                    || TryGetPeriod(resource, "onsetPeriod", out start, out end);

            case "ServiceRequest":
            case "MedicationRequest":
                return TryGetInstant(resource, "authoredOn", out start, out end);

            default:
                // Unknown resource type carrying a date filter — fail closed.
                return false;
        }
    }

    /// <summary>Reads an instant-shaped date property as a (date, date) range.</summary>
    private static bool TryGetInstant(JsonElement element, string propertyName,
        out DateTimeOffset start, out DateTimeOffset end)
    {
        if (element.TryGetProperty(propertyName, out var prop)
            && prop.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(prop.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            start = parsed;
            end = parsed;
            return true;
        }

        start = default;
        end = default;
        return false;
    }

    /// <summary>Reads a Period-shaped property as (start, end). Missing start defaults to <see cref="DateTimeOffset.MinValue"/>; missing end defaults to <see cref="DateTimeOffset.MaxValue"/> (FHIR open-ended period).</summary>
    private static bool TryGetPeriod(JsonElement element, string propertyName,
        out DateTimeOffset start, out DateTimeOffset end)
    {
        start = default;
        end = default;

        if (!element.TryGetProperty(propertyName, out var period) || period.ValueKind != JsonValueKind.Object)
            return false;

        var hasStart = period.TryGetProperty("start", out var startProp)
            && startProp.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(startProp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out start);

        var hasEnd = period.TryGetProperty("end", out var endProp)
            && endProp.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(endProp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out end);

        if (!hasStart && !hasEnd)
            return false;

        // Open-ended periods: clamp to extremes so overlap math works without conditionals at call sites.
        if (!hasStart) start = DateTimeOffset.MinValue;
        if (!hasEnd) end = DateTimeOffset.MaxValue;

        return true;
    }

    /// <summary>Reads Observation/DiagnosticReport <c>effective[x]</c> in any FHIR-permitted shape.</summary>
    private static bool TryGetEffective(JsonElement resource, out DateTimeOffset start, out DateTimeOffset end)
    {
        return TryGetInstant(resource, "effectiveDateTime", out start, out end)
            || TryGetInstant(resource, "effectiveInstant", out start, out end)
            || TryGetPeriod(resource, "effectivePeriod", out start, out end)
            || TryGetTiming(resource, "effectiveTiming", out start, out end);
    }

    /// <summary>
    /// Reads a Timing-shaped property as (start, end) by using either event instants
    /// (min..max) or repeat.boundsPeriod when events are absent.
    /// </summary>
    private static bool TryGetTiming(JsonElement element, string propertyName,
        out DateTimeOffset start, out DateTimeOffset end)
    {
        start = default;
        end = default;

        if (!element.TryGetProperty(propertyName, out var timing) || timing.ValueKind != JsonValueKind.Object)
            return false;

        if (timing.TryGetProperty("event", out var events)
            && events.ValueKind == JsonValueKind.Array)
        {
            var parsedEvents = events.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                    ? (DateTimeOffset?)parsed
                    : null)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToList();

            if (parsedEvents.Count > 0)
            {
                start = parsedEvents.Min();
                end = parsedEvents.Max();
                return true;
            }
        }

        if (timing.TryGetProperty("repeat", out var repeat)
            && repeat.ValueKind == JsonValueKind.Object)
        {
            return TryGetPeriod(repeat, "boundsPeriod", out start, out end);
        }

        return false;
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
