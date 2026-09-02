using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class CqlTextTests
{
    [Fact]
    public void StripComments_PreservesHttpUrlsInValuesetDeclarations()
    {
        const string url = "http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.666.5.307";
        var cql = $"""
            valueset "Encounter Inpatient": '{url}'
            // this is a real comment
            define "Foo": [Encounter]
            """;

        var stripped = CqlText.StripComments(cql);

        Assert.Contains(url, stripped);
        Assert.DoesNotContain("this is a real comment", stripped);
        Assert.Contains("define \"Foo\"", stripped);
    }

    [Fact]
    public void StripComments_PreservesHttpsUrlsAndStripsBlockComments()
    {
        var cql = """
            valueset "Lab": 'https://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1046.274'
            /* block
               comment */
            define "Bar": true
            """;

        var stripped = CqlText.StripComments(cql);

        Assert.Contains("https://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113762.1.4.1046.274", stripped);
        Assert.DoesNotContain("block", stripped);
        Assert.Contains("define \"Bar\"", stripped);
    }

    [Fact]
    public void AchMonthly_DiagnosticReport_rules_require_ip_period_overlap()
    {
        var model = CqlInstanceFilterAnalyzer.Analyze(
            ProfiledMeasureCatalog.ReadBundleJson(
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation));
        var drRules = model.Rules
            .Where(r => string.Equals(r.ResourceType, "DiagnosticReport", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(drRules);
        var summary = string.Join("; ", drRules.Select(DescribeRule));
        Assert.True(
            drRules.All(r => r.Date == CqlInstanceFilterAnalyzer.DateRelation.OverlapsIpPeriod),
            $"ACH Monthly DiagnosticReport rules must overlap IP.period, got: {summary}");
    }

    [Fact]
    public void AchMonthly_MedicationRequest_rules_require_authored_on_during_ip()
    {
        var model = CqlInstanceFilterAnalyzer.Analyze(
            ProfiledMeasureCatalog.ReadBundleJson(
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation));
        var mrRules = model.Rules
            .Where(r => string.Equals(r.ResourceType, "MedicationRequest", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(mrRules);
        var summary = string.Join("; ", mrRules.Select(DescribeRule));
        Assert.True(
            mrRules.All(r => r.Date == CqlInstanceFilterAnalyzer.DateRelation.DuringIpPeriod),
            $"ACH Monthly MedicationRequest rules must be authoredOn during IP.period, got: {summary}");
    }

    private static string DescribeRule(CqlInstanceFilterAnalyzer.CqlInclusionRule rule)
        => $"{rule.ResourceType} Date={rule.Date} catAny=[{string.Join(",", rule.CategoryAnyOf ?? [])}] catNone=[{string.Join(",", rule.CategoryNoneOf ?? [])}] status=[{string.Join(",", rule.StatusAnyOf ?? [])}] ipExists={rule.RequireIpExists} drResultRefs={rule.DiagnosticReportResultsReferenceMatchingObservations} includeAllLinked={rule.IncludeAllWhenAnyObservationLinkedReportExists}";

    [Fact]
    public void ParseValuesetDeclarations_SurviveCommentStripOnEmbeddedMeasures()
    {
        foreach (var measure in new[]
                 {
                     ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
                     ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation,
                     ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
                 })
        {
            var model = CqlMeasureBundleModel.Parse(ProfiledMeasureCatalog.ReadBundleJson(measure));
            Assert.NotEmpty(model.ValueSetCodes);
            Assert.True(
                model.ValueSetCodes.Values.Any(codes => codes.Count > 0),
                $"{measure} should expand at least one valueset after StripComments");
        }
    }
}
