using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    [Theory]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation, "2.0.0-cibuild")]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation, "2.0.0-cibuild")]
    public void Embedded_system_ach_bundles_are_validation_2_0(ProfiledMeasureType family, string expectedVersion)
    {
        var json = ProfiledMeasureCatalog.ReadBundleJson(family);
        var parsed = MeasureBundleParser.Parse(json);
        parsed.Version.Should().Be(expectedVersion);
    }

    [Theory]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation)]
    public void Embedded_system_ach_bundles_include_every_cql_valueset(ProfiledMeasureType family)
    {
        using var doc = JsonDocument.Parse(ProfiledMeasureCatalog.ReadBundleJson(family));
        var vsUrls = new HashSet<string>(StringComparer.Ordinal);
        var cql = new StringBuilder();
        foreach (var entry in doc.RootElement.GetProperty("entry").EnumerateArray())
        {
            if (!entry.TryGetProperty("resource", out var resource))
                continue;
            var type = resource.GetProperty("resourceType").GetString();
            if (type == "ValueSet" && resource.TryGetProperty("url", out var url))
                vsUrls.Add(url.GetString()!);
            if (type != "Library" || !resource.TryGetProperty("content", out var content))
                continue;
            foreach (var attachment in content.EnumerateArray())
            {
                var contentType = attachment.TryGetProperty("contentType", out var ct) ? ct.GetString() : null;
                if (contentType is null || !contentType.StartsWith("text/cql", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!attachment.TryGetProperty("data", out var data))
                    continue;
                cql.AppendLine(Encoding.UTF8.GetString(Convert.FromBase64String(data.GetString()!)));
            }
        }

        var missing = Regex.Matches(cql.ToString(), """valueset "[^"]+": '([^']+)'""")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Where(u => !vsUrls.Contains(u))
            .ToList();

        missing.Should().BeEmpty("CQL valueset URLs must be present in the evaluation bundle");
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
