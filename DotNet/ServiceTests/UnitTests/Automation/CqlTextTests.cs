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
