namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Per-measure resolution of which encounter periods constitute the Initial Population
/// (IP) for a patient. CQL retrieves of the form
/// <c>where exists "Initial Population" IP where Resource.X overlaps IP.period</c> are
/// modeled by computing the IP windows once per simulator call and then having each
/// profile test its resources against those windows.
///
/// The resolver currently uses encounter <c>class.code</c> as the IP-membership
/// predicate, mirroring the value-set bound retrieves the CQL libraries declare:
/// <list type="bullet">
///   <item>ACH Monthly + ACH Daily — class in {IMP, EMER, AMB} (inpatient, ED, observation/short-stay)</item>
///   <item>Hypoglycemic — class IMP (inpatient encounter)</item>
/// </list>
///
/// Encounters that don't match any selected measure's class predicate are excluded
/// from the IP set; the simulator's Encounter profile additionally drops encounters
/// that don't overlap any IP window (matching the "SDE Encounter overlaps IP.period"
/// CQL retrieve).
/// </summary>
public static class MeasureInitialPopulationResolver
{
    /// <summary>
    /// A single Initial-Population encounter period. <see cref="EncounterId"/> is preserved
    /// so profiles that need to match an <c>Encounter.reference</c> against the IP set
    /// (e.g. Condition <c>encounter-diagnosis</c>) can do so without re-walking encounters.
    /// </summary>
    public sealed record IpWindow(string EncounterId, DateTime Start, DateTime End);

    /// <summary>
    /// Returns the union of IP windows across the supplied measures. An encounter is
    /// included in the result if it matches the IP-membership predicate of <b>any</b>
    /// supplied measure. Duplicate encounters (by id) are deduplicated.
    ///
    /// When the patient's input has no encounter list (legacy single-encounter shape),
    /// returns an empty list — the caller falls back to the legacy <c>EncounterStart/End</c>
    /// triple. This preserves pre-Landing-3 behavior for tests that build a
    /// <c>PatientCqlInput</c> via the positional constructor.
    /// </summary>
    public static IReadOnlyList<IpWindow> Resolve(
        IReadOnlyList<ProfiledMeasureType> measures,
        CqlFilterSimulator.PatientCqlInput input)
    {
        if (input.Encounters == null || input.Encounters.Count == 0)
            return Array.Empty<IpWindow>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<IpWindow>();

        foreach (var enc in input.Encounters)
        {
            if (enc == null || string.IsNullOrEmpty(enc.EncounterId)) continue;
            if (!IsIpForAnyMeasure(enc, measures)) continue;
            if (!OverlapsMeasurementPeriod(enc, input)) continue;
            if (!seen.Add(enc.EncounterId)) continue;

            result.Add(new IpWindow(enc.EncounterId, enc.PeriodStart, enc.PeriodEnd));
        }

        return result;
    }

    /// <summary>
    /// True when the encounter's class code qualifies it as IP for any of the given measures.
    /// </summary>
    private static bool IsIpForAnyMeasure(CqlFilterSimulator.EncounterContext enc, IReadOnlyList<ProfiledMeasureType> measures)
    {
        foreach (var measure in measures)
        {
            if (IsIpForMeasure(enc, measure))
                return true;
        }
        return false;
    }

    private static bool IsIpForMeasure(CqlFilterSimulator.EncounterContext enc, ProfiledMeasureType measure)
    {
        var cls = enc.ClassCode ?? string.Empty;
        return measure switch
        {
            // ACH measures: inpatient, emergency, observation/short-stay (AMB) all qualify.
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation or
            ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation =>
                MatchesClass(cls, "IMP", "EMER", "AMB"),

            // Hypoglycemic: inpatient encounter only.
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation =>
                MatchesClass(cls, "IMP"),

            _ => false
        };
    }

    private static bool MatchesClass(string cls, params string[] accepted)
    {
        foreach (var a in accepted)
        {
            if (string.Equals(cls, a, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// True when the encounter overlaps the patient's configured measurement period (or
    /// when the input doesn't carry one — back-compat default of <c>MinValue/MaxValue</c>).
    /// </summary>
    private static bool OverlapsMeasurementPeriod(CqlFilterSimulator.EncounterContext enc, CqlFilterSimulator.PatientCqlInput input)
    {
        var mpStart = input.MeasurementPeriodStart;
        var mpEnd = input.MeasurementPeriodEnd;
        if (mpStart == DateTime.MinValue && mpEnd == DateTime.MaxValue)
            return true;

        return enc.PeriodStart <= mpEnd && enc.PeriodEnd >= mpStart;
    }
}

/// <summary>Helpers that profiles call to test resources against a set of IP windows.</summary>
public static class IpWindowExtensions
{
    /// <summary>True when any IP window overlaps the closed interval [start, end].</summary>
    public static bool AnyOverlaps(this IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> windows, DateTime start, DateTime end)
    {
        if (windows == null) return false;
        for (var i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (start <= w.End && end >= w.Start) return true;
        }
        return false;
    }

    /// <summary>True when any IP window contains the instant <paramref name="instant"/>.</summary>
    public static bool AnyContains(this IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> windows, DateTime instant)
    {
        if (windows == null) return false;
        for (var i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (instant >= w.Start && instant <= w.End) return true;
        }
        return false;
    }

    /// <summary>True when any IP window's end is strictly after <paramref name="date"/> (i.e. <paramref name="date"/> is before the IP end).</summary>
    public static bool AnyEndStrictlyAfter(this IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> windows, DateTime date)
    {
        if (windows == null) return false;
        for (var i = 0; i < windows.Count; i++)
        {
            if (date < windows[i].End.Date) return true;
        }
        return false;
    }

    /// <summary>True when any IP window's end is on or after <paramref name="date"/>.</summary>
    public static bool AnyEndOnOrAfter(this IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> windows, DateTime date)
    {
        if (windows == null) return false;
        for (var i = 0; i < windows.Count; i++)
        {
            if (date <= windows[i].End.Date) return true;
        }
        return false;
    }

    /// <summary>
    /// True when <paramref name="encounterReference"/> resolves to an <c>EncounterId</c>
    /// that matches at least one IP window. Accepts both <c>"Encounter/{id}"</c> and bare
    /// <c>"{id}"</c> reference shapes.
    /// </summary>
    public static bool AnyEncounterMatches(this IReadOnlyList<MeasureInitialPopulationResolver.IpWindow> windows, string? encounterReference)
    {
        if (windows == null || string.IsNullOrWhiteSpace(encounterReference)) return false;
        var slash = encounterReference.IndexOf('/');
        var refId = slash >= 0 ? encounterReference[(slash + 1)..] : encounterReference;
        for (var i = 0; i < windows.Count; i++)
        {
            if (string.Equals(refId, windows[i].EncounterId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
