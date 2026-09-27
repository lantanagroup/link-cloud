using FluentAssertions;
using LantanaGroup.Automation;
using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class FhirAcquisitionReadinessTests
{
    private static readonly ProfiledMeasureType Ach = ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation;

    [Fact]
    public void BuildProbes_samples_submitted_patients_and_prefers_observation_search()
    {
        var manifest = new GenerationManifest
        {
            PatientIds = ["p-1", "p-2", "p-nq"],
            Profiles =
            [
                Qualifying(),
                Qualifying(),
                new PatientProfile(new Dictionary<ProfiledMeasureType, MeasureEligibility>
                {
                    [Ach] = MeasureEligibility.NonQualifying
                })
            ],
            SelectedMeasures = [Ach],
            ResourceCountsByPatientType = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal)
            {
                ["p-1"] = new(StringComparer.OrdinalIgnoreCase) { ["Observation"] = 31, ["Encounter"] = 1 },
                ["p-2"] = new(StringComparer.OrdinalIgnoreCase) { ["Encounter"] = 1 }
            }
        };

        var probes = FhirAcquisitionReadiness.BuildProbes(manifest, maxPatients: 3);

        probes.Should().HaveCount(2);
        probes[0].PatientId.Should().Be("p-1");
        probes[0].SearchResourceType.Should().Be("Observation");
        probes[0].ExpectedSearchCount.Should().Be(31);
        probes[1].PatientId.Should().Be("p-2");
        probes[1].SearchResourceType.Should().Be("Encounter");
    }

    [Fact]
    public void ParseSearchBundleTotal_reads_bundle_total()
    {
        FhirDataLoader.ParseSearchBundleTotal("""{"resourceType":"Bundle","type":"searchset","total":31}""")
            .Should().Be(31);
    }

    [Fact]
    public void ParseSearchBundleTotal_falls_back_to_entry_count()
    {
        FhirDataLoader.ParseSearchBundleTotal(
                """{"resourceType":"Bundle","type":"searchset","entry":[{"resource":{"resourceType":"Observation"}}]}""")
            .Should().Be(1);
    }

    private static PatientProfile Qualifying() =>
        new(new Dictionary<ProfiledMeasureType, MeasureEligibility>
        {
            [Ach] = MeasureEligibility.Qualifying
        });
}
