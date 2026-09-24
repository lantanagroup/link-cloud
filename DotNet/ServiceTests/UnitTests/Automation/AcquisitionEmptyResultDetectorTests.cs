using FluentAssertions;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Validation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class AcquisitionEmptyResultDetectorTests
{
    private static readonly ProfiledMeasureType Ach = ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation;

    [Fact]
    public void ResourceId_matches_generated_patient_prefixed_ids()
    {
        AcquisitionEmptyResultDetector.ResourceIdBelongsToPatient(
            "Observation/Patient-d3e40abc-025-Observation-002",
            "Patient-d3e40abc-025").Should().BeTrue();

        AcquisitionEmptyResultDetector.ResourceIdBelongsToPatient(
            "Observation/Patient-d3e40abc-0250-Observation-002",
            "Patient-d3e40abc-025").Should().BeFalse();

        AcquisitionEmptyResultDetector.ResourceIdBelongsToPatient(
            "Patient/Patient-d3e40abc-025",
            "Patient-d3e40abc-025").Should().BeTrue();
    }

    [Fact]
    public void Empty_acquisition_is_flagged_when_manifest_expected_observations()
    {
        var manifest = QualifyingManifest(
            "Patient-d3e40abc-025",
            simulatedKeys: ["Observation/Patient-d3e40abc-025-Observation-002", "Encounter/Patient-d3e40abc-025-Encounter-001"]);

        var findings = AcquisitionEmptyResultDetector.Find(
            manifest,
            ["Encounter/Patient-d3e40abc-025-Encounter-001"],
            [CompletedLog("Patient-d3e40abc-025")]);

        findings.Should().ContainSingle(f =>
            f.PatientId == "Patient-d3e40abc-025"
            && f.ResourceType == "Observation"
            && f.ExpectedCount == 1
            && f.ActualCount == 0);
    }

    [Fact]
    public void Acquired_observations_do_not_flag_empty_acquisition()
    {
        var manifest = QualifyingManifest(
            "Patient-d3e40abc-025",
            simulatedKeys: ["Observation/Patient-d3e40abc-025-Observation-002"]);

        var findings = AcquisitionEmptyResultDetector.Find(
            manifest,
            ["Observation/Patient-d3e40abc-025-Observation-002"],
            [CompletedLog("Patient-d3e40abc-025")]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void Reference_query_types_are_not_treated_as_empty_acquisition()
    {
        var manifest = QualifyingManifest(
            "p-1",
            simulatedKeys: ["Location/loc-1", "Observation/p-1-Observation-001"]);

        var findings = AcquisitionEmptyResultDetector.Find(
            manifest,
            ["Observation/p-1-Observation-001"],
            [CompletedLog("p-1")]);

        findings.Should().BeEmpty("Location is a reference query and can legitimately return fewer results");
    }

    [Fact]
    public void Not_reportable_patients_are_skipped()
    {
        var manifest = QualifyingManifest(
            "p-nr",
            simulatedKeys: ["Observation/p-nr-Observation-001"]);

        var findings = AcquisitionEmptyResultDetector.Find(
            manifest,
            acquiredResourceIds: [],
            logs:
            [
                new PipelineDataReader.AcquisitionLogInfo(
                    1, "p-nr", null, null, "NotReportable", "Initial", [], [], [])
            ]);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void Non_qualifying_patients_are_not_checked()
    {
        var manifest = new GenerationManifest
        {
            PatientIds = ["p-nq"],
            Profiles =
            [
                new PatientProfile(new Dictionary<ProfiledMeasureType, MeasureEligibility>
                {
                    [Ach] = MeasureEligibility.NonQualifying
                })
            ],
            SelectedMeasures = [Ach],
            ParameterQueryResourceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Observation" },
            SimulatedAcquiredResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["p-nq"] = new(StringComparer.OrdinalIgnoreCase) { "Observation/p-nq-Observation-001" }
            }
        };

        var findings = AcquisitionEmptyResultDetector.Find(manifest, [], [CompletedLog("p-nq")]);
        findings.Should().BeEmpty();
    }

    private static GenerationManifest QualifyingManifest(string patientId, IReadOnlyList<string> simulatedKeys)
    {
        return new GenerationManifest
        {
            PatientIds = [patientId],
            Profiles =
            [
                new PatientProfile(new Dictionary<ProfiledMeasureType, MeasureEligibility>
                {
                    [Ach] = MeasureEligibility.Qualifying
                })
            ],
            SelectedMeasures = [Ach],
            ParameterQueryResourceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Encounter", "Observation", "Condition"
            },
            SimulatedAcquiredResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                [patientId] = new(simulatedKeys, StringComparer.OrdinalIgnoreCase)
            }
        };
    }

    private static PipelineDataReader.AcquisitionLogInfo CompletedLog(string patientId) =>
        new(1, patientId, null, null, "Completed", "Supplemental", [], [], []);
}
