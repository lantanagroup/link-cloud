using LantanaGroup.Automation.Generation;
using Xunit;

namespace UnitTests.Automation;

/// <summary>
/// Parity with the Automation.UI Generation Manifest page (Views/Runs/Manifest.cshtml, renderComparison and
/// renderEligibility). The oracle below is a line-for-line C# transcription of that JavaScript; every input
/// in the grid must give the same verdict from the ported Automation rule.
/// </summary>
[Trait("Category", "UnitTests")]
public class AbsPredictionComparisonParityTests
{
    // --- Oracle: Automation.UI renderComparison, per-type row (verbatim order) ---
    private static string OldTypeVerdict(int g, int e, int a)
    {
        var d = a - e;
        if (d == 0 && e == a) return "Match";
        if (a > e) return "ABS has more";
        if (e > 0 && a == 0) return "Missing from ABS";
        if (a < e) return "Partial";
        if (g > 0 && e == 0) return "Filtered out";
        return "\u2014";
    }

    // --- Oracle: Automation.UI renderComparison, per-patient row ---
    private static string OldPatientVerdict(bool inAbs, int eTotal, int aTotal)
    {
        var d = aTotal - eTotal;
        if (!inAbs) return "Missing from ABS";
        if (d == 0) return "Match";
        if (d > 0) return "ABS has more"; // old page rendered "+N" with the "more" icon
        return "Partial";                 // old page rendered "-N" with the warning icon
    }

    private static bool OldQpAcq(string t, HashSet<string> acquiredLower)
    {
        var tl = t.ToLowerInvariant();
        return acquiredLower.Count == 0 || acquiredLower.Contains(tl) || tl == "patient";
    }

    public static IEnumerable<object[]> Grid()
    {
        for (var g = 0; g <= 3; g++)
            for (var e = 0; e <= 3; e++)
                for (var a = 0; a <= 3; a++)
                    yield return [g, e, a];
    }

    [Theory]
    [MemberData(nameof(Grid))]
    public void Type_verdict_matches_Automation_UI(int generated, int predicted, int actual) =>
        Assert.Equal(OldTypeVerdict(generated, predicted, actual), AbsPredictionComparison.TypeVerdict(generated, predicted, actual));

    [Theory]
    [MemberData(nameof(Grid))]
    public void Patient_verdict_matches_Automation_UI(int presentFlag, int predicted, int actual)
    {
        var present = presentFlag % 2 == 0;
        Assert.Equal(OldPatientVerdict(present, predicted, actual), AbsPredictionComparison.PatientVerdict(present, predicted, actual));
    }

    [Theory]
    [InlineData("Observation", new[] { "Observation", "Encounter" })]
    [InlineData("observation", new[] { "Observation" })]
    [InlineData("Patient", new[] { "Encounter" })]
    [InlineData("Condition", new[] { "Encounter" })]
    [InlineData("Condition", new string[0])]
    public void Query_plan_and_cql_flags_match_Automation_UI(string type, string[] configured)
    {
        var old = OldQpAcq(type, configured.Select(t => t.ToLowerInvariant()).ToHashSet());
        var set = new HashSet<string>(configured, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(old, AbsPredictionComparison.QueryPlanAcquires(type, set));
        Assert.Equal(old, AbsPredictionComparison.CqlReferences(type, set));
    }

    [Fact]
    public void Qualifying_count_matches_Automation_UI_eligibility_badge()
    {
        var eligibility = new Dictionary<string, List<string>>
        {
            ["p1"] = ["NhsnAcuteCareHospitalMonthlyInitialPopulation"],
            ["p2"] = [],
            ["p3"] = ["NhsnAcuteCareHospitalMonthlyInitialPopulation", "NhsnGlycemicControlHypoglycemicInitialPopulation"]
        };
        // Old: `${entries.filter(([, m]) => m.length > 0).length}/${entries.length} qualifying`
        Assert.Equal((2, 3), AbsPredictionComparison.Qualifying(eligibility));
        Assert.Equal((0, 0), AbsPredictionComparison.Qualifying(null));
    }

    [Theory]
    [InlineData(100, 80, 20)]
    [InlineData(80, 80, 0)]
    [InlineData(70, 80, 0)] // old page showed -10 here: a display bug, now floored
    public void Filtered_is_generated_minus_predicted_floored_at_zero(int generated, int predicted, int expected) =>
        Assert.Equal(expected, AbsPredictionComparison.Filtered(generated, predicted));

    [Fact]
    public void Actual_total_prefers_the_upload_total_like_Automation_UI()
    {
        var counts = new Dictionary<string, int> { ["Observation"] = 5, ["Encounter"] = 2 };
        Assert.Equal(9, AbsPredictionComparison.ActualTotal(9, counts)); // abs.totalResourceCount || sum
        Assert.Equal(7, AbsPredictionComparison.ActualTotal(0, counts));
        Assert.Equal(0, AbsPredictionComparison.ActualTotal(0, null));
    }

    /// <summary>
    /// The live Multi Measure run (c56fe6b2): patient 001 qualifies for both measures, 002 for ACH only.
    /// Automation.UI listed each patient's own measures; Link.UI had credited every patient to the first measure.
    /// </summary>
    [Fact]
    public void Multi_measure_prediction_counts_each_measure_separately()
    {
        var eligibility = new Dictionary<string, List<string>>
        {
            ["Patient-f2873c35-001"] = ["NhsnAcuteCareHospitalMonthlyInitialPopulation", "NhsnGlycemicControlHypoglycemicInitialPopulation"],
            ["Patient-f2873c35-002"] = ["NhsnAcuteCareHospitalMonthlyInitialPopulation"]
        };
        var ids = new[] { "Patient-f2873c35-001", "Patient-f2873c35-002" };
        var actual = new HashSet<string>(ids);
        var rows = AbsPredictionComparison.ByMeasure(eligibility, ids,
            ["NHSNAcuteCareHospitalMonthlyInitialPopulation", "NHSNGlycemicControlHypoglycemicInitialPopulation"], actual);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new AbsPredictionComparison.MeasurePrediction("NHSNAcuteCareHospitalMonthlyInitialPopulation", 2, 0, 2, 0), rows[0]);
        Assert.Equal(new AbsPredictionComparison.MeasurePrediction("NHSNGlycemicControlHypoglycemicInitialPopulation", 1, 1, 1, 0), rows[1]);
        Assert.True(AbsPredictionComparison.Qualifies(eligibility, "Patient-f2873c35-002", "nhsnacutecarehospitalmonthlyinitialpopulation"));
        Assert.False(AbsPredictionComparison.Qualifies(eligibility, "Patient-f2873c35-002", "NHSNGlycemicControlHypoglycemicInitialPopulation"));
    }

    [Fact]
    public void Single_measure_prediction_equals_the_old_qualifying_count()
    {
        var eligibility = new Dictionary<string, List<string>>
        {
            ["p1"] = ["NhsnAcuteCareHospitalMonthlyInitialPopulation"],
            ["p2"] = [],
            ["p3"] = ["NhsnAcuteCareHospitalMonthlyInitialPopulation"]
        };
        var row = Assert.Single(AbsPredictionComparison.ByMeasure(eligibility, eligibility.Keys.ToList(), ["NhsnAcuteCareHospitalMonthlyInitialPopulation"], new HashSet<string> { "p1" }));
        var (qualifying, total) = AbsPredictionComparison.Qualifying(eligibility);
        Assert.Equal(qualifying, row.Predicted);
        Assert.Equal(total - qualifying, row.NotPredicted);
        Assert.Equal(1, row.InActual);
        Assert.Equal(1, row.MissingFromActual);
    }
}