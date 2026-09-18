namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Places a generated encounter against a report period using a saved stay pattern.
/// This is the single formula used by generation and by the configuration UI preview.
/// </summary>
public static class ScheduledStayWindow
{
    public static readonly ScheduledInpatientPattern DefaultPattern =
        ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod;

    public static (DateTime Start, DateTime End) Compute(
        ScheduledInpatientPattern pattern,
        DateTime reportStart,
        DateTime reportEnd,
        int seed = 0)
    {
        var rs = DateTime.SpecifyKind(reportStart, DateTimeKind.Utc);
        var re = DateTime.SpecifyKind(reportEnd, DateTimeKind.Utc);
        if (re <= rs)
            return FhirBundleGenerator.DeriveInpatientEncounterWindow(seed, rs, re);

        var period = re - rs;
        var totalMinutes = Math.Max(1, (int)period.TotalMinutes);
        var admissionOffsetMinutes = Math.Max(5, (int)Math.Round(totalMinutes * 0.20));
        var dischargeOffsetMinutes = Math.Max(admissionOffsetMinutes + 30, (int)Math.Round(totalMinutes * 0.75));
        var jitter = Math.Abs(seed % 20);

        var inPeriodStart = rs.AddMinutes(Math.Min(totalMinutes - 1, admissionOffsetMinutes + jitter));
        var inPeriodEnd = rs.AddMinutes(Math.Min(totalMinutes - 1, dischargeOffsetMinutes + jitter));
        if (inPeriodEnd <= inPeriodStart)
            inPeriodEnd = inPeriodStart.AddMinutes(30);

        var boundaryPad = period.TotalHours >= 12
            ? TimeSpan.FromHours(6)
            : TimeSpan.FromMinutes(Math.Max(60, totalMinutes / 6));

        return pattern switch
        {
            ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod
                => (rs - boundaryPad, re + boundaryPad),
            ScheduledInpatientPattern.AdmittedBeforePeriodDischargedDuringPeriod
                => (rs - boundaryPad, inPeriodEnd),
            ScheduledInpatientPattern.AdmittedDuringPeriodRemainsInpatientAfterPeriod
                => (inPeriodStart, re + boundaryPad),
            ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod
                => (inPeriodStart, inPeriodEnd),
            ScheduledInpatientPattern.AdmittedAndDischargedBeforePeriod
                => (rs - (boundaryPad + TimeSpan.FromHours(6)), rs - TimeSpan.FromHours(1)),
            ScheduledInpatientPattern.AdmittedAndDischargedAfterPeriod
                => (re + TimeSpan.FromHours(1), re + (boundaryPad + TimeSpan.FromHours(6))),
            _ => FhirBundleGenerator.DeriveInpatientEncounterWindow(seed, rs, re)
        };
    }

    public static bool TryCompute(
        ScheduledInpatientPattern? pattern,
        DateTime? reportStart,
        DateTime? reportEnd,
        int seed,
        out DateTime start,
        out DateTime end)
    {
        start = default;
        end = default;
        if (!reportStart.HasValue || !reportEnd.HasValue || reportEnd.Value <= reportStart.Value)
            return false;

        (start, end) = Compute(pattern ?? DefaultPattern, reportStart.Value, reportEnd.Value, seed);
        return true;
    }

    public static IReadOnlyList<(string Value, string Label, string Hint, bool ExpectedInReport)> Catalog()
        => Enum.GetValues<ScheduledInpatientPattern>()
            .Select(p => (
                p.ToString(),
                p.GetUiShortLabel(),
                p.GetUiHint(),
                p.GetCensusBehavior().ExpectedInReport))
            .ToList();
}
