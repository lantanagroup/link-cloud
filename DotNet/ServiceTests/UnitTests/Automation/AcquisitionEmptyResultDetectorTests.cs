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
    public void Imported_uuid_ids_do_not_match_the_generated_patient_prefix()
    {
        const string patientId = "01a0cfc5-e9f7-762b-a544-debb1c474a5b";
        var manifest = QualifyingManifest(
            patientId,
            simulatedKeys:
            [
                "Observation/01a0cfc5-ea49-7ffb-8795-5aa58de0ba50",
                "Encounter/01a0cfc5-ea33-7f81-8f12-c56aa79b4416"
            ]);

        var findings = AcquisitionEmptyResultDetector.Find(
            manifest,
            [
                "Observation/01a0cfc5-ea49-7ffb-8795-5aa58de0ba50",
                "Encounter/01a0cfc5-ea33-7f81-8f12-c56aa79b4416"
            ],
            [CompletedLog(patientId)]);

        findings.Select(f => f.ResourceType).Should().BeEquivalentTo("Observation", "Encounter");
    }

    [Fact]
    public void Imported_resources_count_by_the_acquisition_log_patient()
    {
        const string patientId = "01a0cfc5-e9f7-762b-a544-debb1c474a5b";
        var manifest = QualifyingManifest(
            patientId,
            simulatedKeys:
            [
                "Observation/01a0cfc5-ea49-7ffb-8795-5aa58de0ba50",
                "Encounter/01a0cfc5-ea33-7f81-8f12-c56aa79b4416"
            ]);

        var findings = AcquisitionEmptyResultDetector.Find(
            manifest,
            acquiredResourceIds: [],
            logs: [CompletedLog(patientId)],
            acquiredByPatient:
            [
                new PipelineDataReader.PatientResourceTypeCount(patientId, "Observation", 129),
                new PipelineDataReader.PatientResourceTypeCount(patientId, "Encounter", 8)
            ]);

        findings.Should().BeEmpty();
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
