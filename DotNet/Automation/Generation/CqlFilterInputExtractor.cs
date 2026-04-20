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

    private static DateTime? ParseFhirDateTime(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return parsed;
        return null;
    }
}
