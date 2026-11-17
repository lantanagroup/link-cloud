using Xunit;

namespace Link.UI.Tests;

/// <summary>
/// Prediction and validation are Automation domain logic. Link.UI only pages, sorts and styles their results.
/// </summary>
public class PredictionValidationPlacementTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Link.UI")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    [Fact]
    public void Manifest_rules_take_every_verdict_from_Automation()
    {
        var rules = File.ReadAllText(Path.Combine(Root(), "Link.UI", "Services", "ReportManifestRules.cs"));
        foreach (var call in new[] { "AbsPredictionComparison.TypeVerdict(", "AbsPredictionComparison.PatientVerdict(", "AbsPredictionComparison.QueryPlanAcquires(", "AbsPredictionComparison.CqlReferences(", "AbsPredictionComparison.Filtered(", "AbsPredictionComparison.ActualTotal(" })
            Assert.Contains(call, rules);
        // No verdict is decided in the UI: the literals only appear in Automation.
        Assert.DoesNotContain("\"Missing from ABS\"", rules);
        Assert.DoesNotContain("\"ABS has more\"", rules);
        Assert.DoesNotContain("key == \"patient\"", rules);
    }

    [Fact]
    public void No_validator_or_prediction_code_lives_in_the_UI_engine()
    {
        var engine = Path.Combine(Root(), "Link.UI", "Engine");
        var offenders = Directory.EnumerateFiles(engine, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine("ApiHealth", "TestSuites")))
            .Select(Path.GetFileName)
            .Where(name => name!.Contains("Validator") || name.Contains("Predict") || name.Contains("ExpectedState") || name.Contains("ExecutionSummaryParser") || name.Contains("RuntimeSequencePlanner"))
            .ToList();
        Assert.True(offenders.Count == 0, "Move to Automation.Link: " + string.Join(", ", offenders));
        foreach (var moved in new[] { "ValidatorRunner.cs", "NormalizationSuiteApplicationValidator.cs", "NormalizationRuntimeSequencePlanner.cs", "NormalizationExecutionSummaryParser.cs" })
            Assert.True(File.Exists(Path.Combine(Root(), "Automation.Link", "Validation", moved)), moved);
        Assert.True(File.Exists(Path.Combine(Root(), "Automation.Link", "Live", "LiveExpectedStateTracker.cs")));
        Assert.True(File.Exists(Path.Combine(Root(), "Automation", "Generation", "AbsPredictionComparison.cs")));
    }
}