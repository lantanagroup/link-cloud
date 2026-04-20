using Hl7.Fhir.Model;
using System.Globalization;
using System.Text.Json;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Extracts <see cref="CqlFilterSimulator.PatientCqlInput"/> from generated FHIR content —
/// either from in-memory <see cref="Bundle.EntryComponent"/> lists (pipeline path) or from
/// serialized transaction bundle JSON (batch/test path).
///
/// Both code paths feed the simulator identical data shapes (actual Condition attributes +
/// actual Encounter period), so prediction can never drift from the generator's internals.
/// </summary>
public static class CqlFilterInputExtractor
{
    // ---------- In-memory extraction (pipeline) ----------

    /// <summary>
    /// Builds a <see cref="CqlFilterSimulator.PatientCqlInput"/> from a patient's in-memory
    /// generated FHIR entries. Returns <c>null</c> if the patient has no encounter recorded.
    /// </summary>
    public static CqlFilterSimulator.PatientCqlInput? ExtractFromEntries(
        string patientId,
        IEnumerable<Bundle.EntryComponent> entries)
    {
        Encounter? encounter = null;
        var conditions = new List<Condition>();

        foreach (var entry in entries)
        {
            switch (entry.Resource)
            {
                case Encounter enc when encounter == null:
                    encounter = enc;
                    break;
                case Condition cond:
                    conditions.Add(cond);
                    break;
            }
        }

        if (encounter == null || encounter.Period == null)
            return null;

        var encStart = ParseFhirDateTime(encounter.Period.Start) ?? DateTime.MinValue;
        var encEnd = ParseFhirDateTime(encounter.Period.End) ?? DateTime.MaxValue;
        var encounterId = encounter.Id;

        var contexts = new List<CqlFilterSimulator.ConditionContext>(conditions.Count);
        foreach (var cond in conditions)
        {
            contexts.Add(BuildConditionContext(cond));
        }

        return new CqlFilterSimulator.PatientCqlInput(
            patientId,
            encounterId,
            encStart,
            encEnd,
            contexts);
    }

    private static CqlFilterSimulator.ConditionContext BuildConditionContext(Condition cond)
    {
        var isActive = cond.ClinicalStatus?.Coding?
            .Any(c => string.Equals(c.Code, "active", StringComparison.OrdinalIgnoreCase)) ?? false;

        var categories = (cond.Category ?? [])
            .SelectMany(cat => cat.Coding ?? [])
            .Select(c => c.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var recordedDate = ParseFhirDateTime(cond.RecordedDate) ?? DateTime.MinValue;
        var encounterReference = cond.Encounter?.Reference ?? string.Empty;

        return new CqlFilterSimulator.ConditionContext(
            cond.Id,
            isActive,
            recordedDate.Date,
            encounterReference,
            categories);
    }

    // ---------- JSON bundle extraction (batch / tests) ----------

    /// <summary>
    /// Parses transaction bundle JSON and returns per-patient CQL inputs keyed by patient id.
    /// Patients whose bundles contain no Encounter are omitted.
    /// </summary>
    public static Dictionary<string, CqlFilterSimulator.PatientCqlInput> ExtractFromBundles(
        IReadOnlyList<string> patientIds,
        IReadOnlyList<(string Name, string Json)> bundles)
    {
        var result = new Dictionary<string, CqlFilterSimulator.PatientCqlInput>(StringComparer.Ordinal);
        if (patientIds == null || patientIds.Count == 0 || bundles == null || bundles.Count == 0)
            return result;

        // Per-patient accumulators
        var encounters = new Dictionary<string, (string EncounterId, DateTime Start, DateTime End)>(StringComparer.Ordinal);
        var conditionsByPatient = new Dictionary<string, List<CqlFilterSimulator.ConditionContext>>(StringComparer.Ordinal);

        foreach (var (_, json) in bundles)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entry", out var entryArr) || entryArr.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var entry in entryArr.EnumerateArray())
            {
                if (!entry.TryGetProperty("resource", out var resource) || resource.ValueKind != JsonValueKind.Object)
                    continue;

                var resourceType = GetString(resource, "resourceType");
                if (string.IsNullOrEmpty(resourceType))
                    continue;

                var id = GetString(resource, "id") ?? string.Empty;
                var patientId = FindOwnerPatientId(id, patientIds);
                if (patientId == null)
                    continue;

                switch (resourceType)
                {
                    case "Encounter":
                        if (!encounters.ContainsKey(patientId))
                        {
                            var period = resource.TryGetProperty("period", out var periodEl) ? periodEl : default;
                            var start = period.ValueKind == JsonValueKind.Object
                                ? ParseFhirDateTime(GetString(period, "start")) ?? DateTime.MinValue
                                : DateTime.MinValue;
                            var end = period.ValueKind == JsonValueKind.Object
                                ? ParseFhirDateTime(GetString(period, "end")) ?? DateTime.MaxValue
                                : DateTime.MaxValue;
                            encounters[patientId] = (id, start, end);
                        }
                        break;

                    case "Condition":
                        if (!conditionsByPatient.TryGetValue(patientId, out var list))
                        {
                            list = [];
                            conditionsByPatient[patientId] = list;
                        }
                        list.Add(BuildConditionContext(resource, id));
                        break;
                }
            }
        }

        foreach (var patientId in patientIds)
        {
            if (!encounters.TryGetValue(patientId, out var enc))
                continue;

            conditionsByPatient.TryGetValue(patientId, out var condList);
            result[patientId] = new CqlFilterSimulator.PatientCqlInput(
                patientId,
                enc.EncounterId,
                enc.Start,
                enc.End,
                condList ?? []);
        }

        return result;
    }

    private static CqlFilterSimulator.ConditionContext BuildConditionContext(JsonElement resource, string id)
    {
        var isActive = false;
        if (resource.TryGetProperty("clinicalStatus", out var cs) && cs.ValueKind == JsonValueKind.Object
            && cs.TryGetProperty("coding", out var csCoding) && csCoding.ValueKind == JsonValueKind.Array)
        {
            foreach (var coding in csCoding.EnumerateArray())
            {
                if (string.Equals(GetString(coding, "code"), "active", StringComparison.OrdinalIgnoreCase))
                {
                    isActive = true;
                    break;
                }
            }
        }

        var categoryCodes = new List<string>();
        if (resource.TryGetProperty("category", out var catArr) && catArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var cat in catArr.EnumerateArray())
            {
                if (cat.ValueKind != JsonValueKind.Object) continue;
                if (!cat.TryGetProperty("coding", out var codingArr) || codingArr.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var coding in codingArr.EnumerateArray())
                {
                    var code = GetString(coding, "code");
                    if (!string.IsNullOrWhiteSpace(code) && !categoryCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
                        categoryCodes.Add(code);
                }
            }
        }

        var recordedDate = ParseFhirDateTime(GetString(resource, "recordedDate")) ?? DateTime.MinValue;

        var encounterReference = string.Empty;
        if (resource.TryGetProperty("encounter", out var encEl) && encEl.ValueKind == JsonValueKind.Object)
            encounterReference = GetString(encEl, "reference") ?? string.Empty;

        return new CqlFilterSimulator.ConditionContext(
            id,
            isActive,
            recordedDate.Date,
            encounterReference,
            categoryCodes);
    }

    private static string? FindOwnerPatientId(string resourceId, IReadOnlyList<string> patientIds)
    {
        foreach (var pid in patientIds)
        {
            if (resourceId.StartsWith(pid, StringComparison.Ordinal))
                return pid;
        }
        return null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(propertyName, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static DateTime? ParseFhirDateTime(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return parsed;
        return null;
    }
}
