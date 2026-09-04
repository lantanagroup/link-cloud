namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Single source of truth for which encounter <c>class.code</c> values qualify an
/// encounter for the Initial Population (IP) of each NHSN measure family.
///
/// <para>
/// Aligned with the CQL retrieves in the system measure libraries
/// (<c>DotNet/Automation/measures/*.json</c>, decoded base64 CQL):
/// </para>
/// <list type="bullet">
///   <item>
///     <b>NHSNAcuteCareHospitalMonthlyInitialPopulation</b> &mdash;
///     <c>"Qualifying Encounters During Measurement Period"</c> retrieves
///     <c>[Encounter: "Encounter Inpatient"] union [Encounter: "Emergency Department Visit"] union
///     [Encounter: "Observation Services"] union [Encounter: class in "NHSN Inpatient Encounter Class Codes"]
///     union [Encounter: class ~ "emergency"] union [Encounter: class ~ "observation encounter"]</c>
///     filtered by <c>status in {'in-progress','finished','triaged','onleave','entered-in-error'}</c>
///     and by <c>period overlaps "Measurement Period"</c>. The IP additionally unions
///     <c>"Encounters with Patient Hospital Locations"</c> &mdash; encounters whose
///     <c>location.location.type</c> is in <c>"Inpatient, Emergency, and Observation Locations"</c>.
///   </item>
///   <item>
///     <b>NHSNAcuteCareHospitalDailyInitialPopulation</b> &mdash; equivalent class/type/location
///     coverage, split across <c>EncounterInpatient</c> and <c>EncounterObservation</c> defines.
///   </item>
///   <item>
///     <b>NHSNGlycemicControlHypoglycemicInitialPopulation</b> &mdash; the inpatient set is
///     <c>[Encounter: class in "NHSN Inpatient Encounter Class Codes"] union [Encounter: "Encounter Inpatient"]</c>
///     unioned with <c>"Encounters with Patient Hospital Locations"</c>, filtered by the
///     same status/measurement-period predicates, AND further requires that an
///     <i>antidiabetic drug</i> (Diabetes Medications value set) was administered or ordered
///     during the encounter's hospitalization window.
///   </item>
/// </list>
///
/// <para>
/// <b>NHSN Inpatient Encounter Class Codes</b> value set (OID 2.16.840.1.113762.1.4.1046.274)
/// expanded against v3-ActCode contains <c>{IMP, ACUTE, NONAC, SS}</c>. Combined with the
/// directly-referenced <c>EMER</c> and <c>OBSENC</c> codes, this yields the canonical class
/// predicates exposed below.
/// </para>
///
/// <para>
/// <b>Known modeling gaps</b> (intentionally not yet replicated &mdash; tracked for iterative
/// closing of the gap between Automation's Q/NQ prediction and the real CQL evaluator):
/// <list type="bullet">
///   <item>Encounter <c>type</c>-based qualification via the FHIR value sets
///         <c>"Encounter Inpatient"</c>, <c>"Emergency Department Visit"</c>, and
///         <c>"Observation Services"</c>. An encounter with no qualifying class but a
///         qualifying type is still IP per CQL.</item>
///   <item>Encounter-location-based qualification: any encounter whose
///         <c>location[].location.type</c> resolves into the
///         <c>"Inpatient, Emergency, and Observation Locations"</c> value set qualifies,
///         regardless of the encounter's class or type.</item>
///   <item>For the Hypoglycemic measure, the additional <i>during-encounter antidiabetic
///         drug</i> requirement is approximated heuristically (see
///         <see cref="DiabetesMedicationCodes"/>); CQL truly evaluates the entire
///         "Diabetes Medications" value set against MedicationRequest / MedicationAdministration
///         resources and a "HospitalizationWithObservationOrEmergency" interval.</item>
/// </list>
/// As a result, Automation's predicted Q/NQ may diverge from MeasureEval results when a
/// patient qualifies via encounter type, location type, or (for Hypo) medication-only
/// pathways. The dominant pathway in generator-produced data is class-code-based, which
/// this helper models accurately; importing externally-authored bundles is where these
/// gaps are most likely to surface.
/// </para>
/// </summary>
public static class EncounterIpClassification
{
    /// <summary>
    /// Which measure family's IP class predicate to evaluate.
    /// </summary>
    public enum IpProfile
    {
        /// <summary>ACH Monthly + ACH Daily &mdash; accepts inpatient + emergency + observation classes.</summary>
        Ach,

        /// <summary>Glycemic Control Hypoglycemic &mdash; accepts inpatient classes only.</summary>
        Hypoglycemic
    }

    /// <summary>
    /// Members of the "NHSN Inpatient Encounter Class Codes" value set
    /// (<c>http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1046.274</c>).
    /// </summary>
    public static readonly IReadOnlyList<string> NhsnInpatientClassCodes =
        new[] { "IMP", "ACUTE", "NONAC", "SS" };

    /// <summary>
    /// v3-ActCode codes the ACH measures additionally accept by direct reference
    /// (<c>EMER</c> = emergency, <c>OBSENC</c> = observation encounter).
    /// </summary>
    public static readonly IReadOnlyList<string> AchAdditionalClassCodes =
        new[] { "EMER", "OBSENC" };

    /// <summary>
    /// FHIR <c>Encounter.status</c> values that qualify an encounter for IP membership
    /// per the NHSN CQL libraries' status filter.
    /// </summary>
    public static readonly IReadOnlyList<string> ValidIpStatuses =
        new[] { "in-progress", "finished", "triaged", "onleave", "entered-in-error" };

    /// <summary>
    /// Hardcoded RxNorm subset of the "Diabetes Medications" value set
    /// (<c>http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1190.58</c>)
    /// matching codes the generator emits today (see <c>FhirGenerationCodes.Medications</c>).
    /// Measure-bundle expansions overlay additional codes via
    /// <see cref="RegisterDiabetesMedicationCodes"/>. Used by the
    /// <see cref="ImportedPatientClassifier"/> heuristic for hypoglycemic-IP detection.
    /// </summary>
    public static readonly IReadOnlyList<string> DiabetesMedicationCodes =
        new[]
        {
            // Insulins (RxNorm) — the only antidiabetics emitted by FhirGenerationCodes.Medications today.
            "1116635", // Insulin glargine 100 UNT/ML Injectable Solution
            "311040",  // Insulin regular 100 UNT/ML Injectable Solution
        };

    /// <summary>
    /// True when the supplied FHIR <c>Encounter.class.code</c> qualifies the encounter
    /// for IP membership under the given measure profile, ignoring status / period and
    /// the unmodeled type/location pathways.
    /// </summary>
    public static bool ClassCodeQualifiesIp(string? classCode, IpProfile profile)
    {
        if (string.IsNullOrWhiteSpace(classCode)) return false;

        if (Matches(classCode, NhsnInpatientClassCodes)) return true;

        if (profile == IpProfile.Ach && Matches(classCode, AchAdditionalClassCodes)) return true;

        return false;
    }

    /// <summary>
    /// True when the FHIR <c>Encounter.status</c> qualifies the encounter for IP
    /// membership per the NHSN CQL status filter. Empty / unknown status is treated as
    /// permissive so that contexts which don't surface status (legacy tests, partial
    /// imports) continue to behave as before.
    /// </summary>
    public static bool IsValidIpEncounterStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return true;
        return Matches(NormalizeStatus(status), ValidIpStatuses);
    }

    /// <summary>
    /// True when the supplied medication code (typically RxNorm) is recognized as an
    /// antidiabetic agent under <see cref="DiabetesMedicationCodes"/>.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> ExtraDiabetesMedicationCodes =
        new(StringComparer.OrdinalIgnoreCase);

    public static void RegisterDiabetesMedicationCodes(IEnumerable<string>? codes)
    {
        if (codes == null)
            return;
        foreach (var code in codes)
        {
            if (!string.IsNullOrWhiteSpace(code))
                ExtraDiabetesMedicationCodes.TryAdd(code.Trim(), 0);
        }
    }

    public static IReadOnlyList<string> KnownDiabetesMedicationCodes()
    {
        if (ExtraDiabetesMedicationCodes.IsEmpty)
            return DiabetesMedicationCodes;
        return DiabetesMedicationCodes
            .Concat(ExtraDiabetesMedicationCodes.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsDiabetesMedicationCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        if (Matches(code, DiabetesMedicationCodes)) return true;
        return ExtraDiabetesMedicationCodes.ContainsKey(code);
    }

    private static bool Matches(string value, IReadOnlyList<string> set)
    {
        for (var i = 0; i < set.Count; i++)
        {
            if (string.Equals(value, set[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Accepts both FHIR-literal kebab-case statuses (<c>in-progress</c>) and the
    /// PascalCase forms produced by some <c>Enum.ToString()</c> paths
    /// (<c>InProgress</c>, <c>EnteredInError</c>).
    /// </summary>
    private static string NormalizeStatus(string status)
    {
        if (status.Contains('-')) return status;

        // Convert PascalCase / camelCase to kebab-case (e.g. EnteredInError -> entered-in-error).
        var sb = new System.Text.StringBuilder(status.Length + 4);
        for (var i = 0; i < status.Length; i++)
        {
            var c = status[i];
            if (i > 0 && char.IsUpper(c))
                sb.Append('-');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
