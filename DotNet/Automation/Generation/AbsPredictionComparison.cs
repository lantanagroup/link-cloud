namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Prediction vs actual ABS comparison rules (the Generation Manifest "Prediction" tab). Pure domain logic:
/// UIs page, sort and style these results but never decide a verdict themselves. Ported from the
/// Automation.UI manifest page (renderComparison / renderEligibility) with the same outcomes.
/// </summary>
public static class AbsPredictionComparison
{
    public const string Match = "Match";
    public const string MissingFromAbs = "Missing from ABS";
    public const string Partial = "Partial";
    public const string AbsHasMore = "ABS has more";
    public const string FilteredOut = "Filtered out";
    public const string NoVerdict = "\u2014";

    /// <summary>Verdict for one resource type: generated, predicted (expected in ABS) and actual ABS counts.</summary>
    public static string TypeVerdict(int generated, int predicted, int actual)
    {
        if (actual == predicted)
            return Match;
        if (actual > predicted)
            return AbsHasMore;
        if (predicted > 0 && actual == 0)
            return MissingFromAbs;
        if (actual < predicted)
            return Partial;
        // Unreachable once the branches above are exhausted; kept so the order matches the original page.
        if (generated > 0 && predicted == 0)
            return FilteredOut;
        return NoVerdict;
    }

    /// <summary>Verdict for one patient: absent from ABS, or the actual total against the predicted total.</summary>
    public static string PatientVerdict(bool presentInAbs, int predictedTotal, int actualTotal)
    {
        if (!presentInAbs)
            return MissingFromAbs;
        var delta = actualTotal - predictedTotal;
        if (delta == 0)
            return Match;
        return delta > 0 ? AbsHasMore : Partial;
    }

    /// <summary>
    /// Resources removed between generation and prediction. The original page could show a negative number
    /// when prediction exceeded generation (shared resources); that was a display bug, so it floors at zero.
    /// </summary>
    public static int Filtered(int generatedTotal, int predictedTotal) => Math.Max(0, generatedTotal - predictedTotal);

    /// <summary>Actual ABS total: the upload's own total when it has one, else the sum of its per-type counts.</summary>
    public static int ActualTotal(int totalResourceCount, IReadOnlyDictionary<string, int>? countsByType) =>
        totalResourceCount > 0 ? totalResourceCount : countsByType?.Values.Sum() ?? 0;

    /// <summary>Does the query plan acquire this type? No query plan types means no filter; Patient always passes.</summary>
    public static bool QueryPlanAcquires(string type, IReadOnlySet<string> acquiredTypes) =>
        acquiredTypes.Count == 0 || acquiredTypes.Contains(type) || string.Equals(type, "Patient", StringComparison.OrdinalIgnoreCase);

    /// <summary>Does the CQL reference this type? Same rule as <see cref="QueryPlanAcquires"/>.</summary>
    public static bool CqlReferences(string type, IReadOnlySet<string> cqlTypes) =>
        cqlTypes.Count == 0 || cqlTypes.Contains(type) || string.Equals(type, "Patient", StringComparison.OrdinalIgnoreCase);

    /// <summary>"N/M qualifying": patients with at least one qualifying measure, out of every patient with an eligibility entry.</summary>
    public static (int Qualifying, int Total) Qualifying(IReadOnlyDictionary<string, List<string>>? eligibility)
    {
        if (eligibility is null)
            return (0, 0);
        return (eligibility.Count(pair => pair.Value is { Count: > 0 }), eligibility.Count);
    }

    /// <summary>One measure's prediction: who the run expects in its Initial Population, and how many reached ABS.</summary>
    public sealed record MeasurePrediction(string Measure, int Predicted, int NotPredicted, int? InActual, int? MissingFromActual);

    /// <summary>
    /// Is the patient predicted to qualify? With a measure, only for that measure (aliases differ in case,
    /// e.g. Nhsn... vs NHSN...); without one, for any measure. Mirrors the eligibility table, which listed
    /// each patient's own qualifying measures.
    /// </summary>
    public static bool Qualifies(IReadOnlyDictionary<string, List<string>> eligibility, string patientId, string? measure = null)
    {
        if (!eligibility.TryGetValue(patientId, out var list) || list is not { Count: > 0 })
            return false;
        return string.IsNullOrWhiteSpace(measure)
            || list.Any(item => string.Equals(item?.Trim(), measure.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Prediction per measure, in the run's measure order, plus any measure that only appears in eligibility.
    /// Each patient counts only toward the measures they qualify for.
    /// </summary>
    public static IReadOnlyList<MeasurePrediction> ByMeasure(
        IReadOnlyDictionary<string, List<string>> eligibility,
        IReadOnlyCollection<string> patientIds,
        IEnumerable<string> measures,
        IReadOnlySet<string>? actualIds)
    {
        var ordered = new List<string>();
        void Add(string? measure)
        {
            var key = measure?.Trim();
            if (!string.IsNullOrEmpty(key) && !ordered.Contains(key, StringComparer.OrdinalIgnoreCase))
                ordered.Add(key);
        }
        foreach (var measure in measures)
            Add(measure);
        foreach (var list in eligibility.Values)
            foreach (var measure in list ?? [])
                Add(measure);

        return ordered.Select(measure =>
        {
            var predicted = patientIds.Count(id => Qualifies(eligibility, id, measure));
            int? inActual = actualIds is null ? null : patientIds.Count(id => Qualifies(eligibility, id, measure) && actualIds.Contains(id));
            return new MeasurePrediction(measure, predicted, Math.Max(0, patientIds.Count - predicted), inActual, inActual is int present ? predicted - present : null);
        }).ToList();
    }
}