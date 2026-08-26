using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Automation.Generation;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class MeasureBundleParserTests
{
    private const string ValidBundle = """
        {
          "resourceType": "Bundle",
          "type": "transaction",
          "entry": [
            {
              "resource": {
                "resourceType": "Measure",
                "id": "NHSNAcuteCareHospitalMonthlyInitialPopulation",
                "url": "http://www.cdc.gov/nhsn/fhirportal/dqm/ig/Measure/NHSNAcuteCareHospitalMonthlyInitialPopulation",
                "version": "2.0.0-cibuild",
                "title": "NHSN dQM Acute Care Hospital Monthly Initial Population",
                "status": "draft",
                "date": "2026-01-08T09:28:49-05:00"
              }
            },
            {
              "resource": {
                "resourceType": "Library",
                "id": "NHSNAcuteCareHospitalMonthlyInitialPopulation"
              }
            }
          ]
        }
        """;

    [Fact]
    public void Parse_extracts_measure_metadata()
    {
        var parsed = MeasureBundleParser.Parse(ValidBundle);

        parsed.MeasureId.Should().Be("NHSNAcuteCareHospitalMonthlyInitialPopulation");
        parsed.Version.Should().Be("2.0.0-cibuild");
        parsed.Status.Should().Be("draft");
        parsed.CanonicalUrl.Should().Contain("Measure/NHSNAcuteCareHospitalMonthlyInitialPopulation");
        parsed.Title.Should().Contain("Monthly");
    }

    [Fact]
    public void Parse_rejects_missing_measure()
    {
        var json = """{"resourceType":"Bundle","entry":[{"resource":{"resourceType":"Library","id":"x"}}]}""";
        var act = () => MeasureBundleParser.Parse(json);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Measure resource*");
    }

    [Fact]
    public void Parse_rejects_non_bundle()
    {
        var act = () => MeasureBundleParser.Parse("""{"resourceType":"Measure","id":"x"}""");
        act.Should().Throw<InvalidOperationException>().WithMessage("*Bundle*");
    }

    [Fact]
    public void System_ids_are_stable_and_map_from_families()
    {
        MeasureTemplateCatalog.SystemIdFor(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            .Should().Be(MeasureTemplateCatalog.AchMonthlyId);
        MeasureTemplateCatalog.SystemIdsFor(
        [
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation,
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation
        ]).Should().Equal(
            MeasureTemplateCatalog.HypoglycemicId,
            MeasureTemplateCatalog.AchMonthlyId);
    }

    [Fact]
    public void NormalizeMeasureSelection_fills_system_ids_from_families()
    {
        var scenario = new TestScenarioDefinition
        {
            SelectedMeasures = [ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation],
            SelectedMeasureIds = []
        };

        scenario.NormalizeMeasureSelection();

        scenario.SelectedMeasureIds.Should().ContainSingle()
            .Which.Should().Be(MeasureTemplateCatalog.AchDailyId);
    }
}
