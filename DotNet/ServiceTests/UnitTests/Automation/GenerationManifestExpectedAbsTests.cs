using FluentAssertions;
using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

/// <summary>
/// Regression coverage for <see cref="GenerationManifest.GetExpectedAbsKeysForPatient"/>
/// and <see cref="GenerationManifest.AllExpectedAbsPatientResourceKeys"/>.
///
/// Background: scenarios that mix qualifying and non-qualifying patients (e.g. an NQ
/// patient cohort combined with imported bundles) tripped a bug where the manifest
/// claimed the NQ patients' generated/acquired resources should appear in ABS. They
/// must not — measure-eval never emits a MeasureReport for an NQ patient, so
/// PatientAggregator never writes a patient-{id}.ndjson artifact and nothing about
/// the patient surfaces in ABS. The validator then flagged every NQ-patient resource
/// as "missing expected resource", failing runs that were configured correctly.
/// </summary>
[Trait("Category", "UnitTests")]
public class GenerationManifestExpectedAbsTests
{
    private static readonly ProfiledMeasureType Ach = ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation;

    private static PatientProfile QualifyingProfile()
        => new(new Dictionary<ProfiledMeasureType, MeasureEligibility>
        {
            [Ach] = MeasureEligibility.Qualifying,
        });

    private static PatientProfile NonQualifyingProfile()
        => new(new Dictionary<ProfiledMeasureType, MeasureEligibility>
        {
            [Ach] = MeasureEligibility.NonQualifying,
        });

    private static GenerationManifest BuildMixedManifest()
    {
        return new GenerationManifest
        {
            PatientIds = ["p-q", "p-nq"],
            Profiles = [QualifyingProfile(), NonQualifyingProfile()],
            SelectedMeasures = [Ach],
            // Both patients have the same kinds of generated resources — only their
            // eligibility differs. The expectation logic must gate on eligibility.
            ResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["p-q"] = new(StringComparer.OrdinalIgnoreCase) { "Encounter/Eq", "Condition/Cq" },
                ["p-nq"] = new(StringComparer.OrdinalIgnoreCase) { "Encounter/Enq", "Condition/Cnq" },
            },
        };
    }

    [Fact]
    public void Qualifying_patient_expected_keys_include_their_resources_and_Patient_anchor()
    {
        var manifest = BuildMixedManifest();

        var keys = manifest.GetExpectedAbsKeysForPatient("p-q");

        keys.Should().BeEquivalentTo(["Patient/p-q", "Encounter/Eq", "Condition/Cq"]);
    }

    [Fact]
    public void Non_qualifying_patient_expected_keys_are_empty()
    {
        // The bug being regressed: NQ patients used to leak their generated/acquired
        // resource keys into the expected-in-ABS set, even though measure-eval never
        // submits them and ABS never receives a patient artifact for them.
        var manifest = BuildMixedManifest();

        var keys = manifest.GetExpectedAbsKeysForPatient("p-nq");

        keys.Should().BeEmpty(
            "non-qualifying patients are filtered out by measure-eval before any ABS artifact is produced");
    }

    [Fact]
    public void All_expected_abs_keys_excludes_non_qualifying_patients()
    {
        var manifest = BuildMixedManifest();

        var allKeys = manifest.AllExpectedAbsPatientResourceKeys();

        allKeys.Should().Contain(["Patient/p-q", "Encounter/Eq", "Condition/Cq"]);
        allKeys.Should().NotContain(["Patient/p-nq", "Encounter/Enq", "Condition/Cnq"]);
    }

    [Fact]
    public void Mixed_qualifying_and_non_qualifying_only_yields_qualifying_keys()
    {
        // Mirrors the failing real-world scenario: an NQ cohort + imported bundles
        // where the imports are qualifying. AllExpectedAbsPatientResourceKeys must
        // contain only the imports' keys plus shared/qualifying resources.
        var manifest = new GenerationManifest
        {
            PatientIds = ["nq-cohort-1", "nq-cohort-2", "imp-q-1"],
            Profiles =
            [
                NonQualifyingProfile(),
                NonQualifyingProfile(),
                QualifyingProfile(),
            ],
            SelectedMeasures = [Ach],
            ResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["nq-cohort-1"] = new(StringComparer.OrdinalIgnoreCase) { "Encounter/E1", "Condition/C1" },
                ["nq-cohort-2"] = new(StringComparer.OrdinalIgnoreCase) { "Encounter/E2", "Condition/C2" },
                ["imp-q-1"]    = new(StringComparer.OrdinalIgnoreCase) { "Encounter/Ei", "Observation/Oi" },
            },
        };

        var allKeys = manifest.AllExpectedAbsPatientResourceKeys();

        allKeys.Should().BeEquivalentTo(["Patient/imp-q-1", "Encounter/Ei", "Observation/Oi"]);
    }

    [Fact]
    public void Multi_measure_patient_qualifying_for_only_one_measure_still_yields_keys()
    {
        // Patient is NQ for ACH but Q for Hypo — they qualify for at least one selected
        // measure, so their resources must still be expected in ABS.
        var hypo = ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation;
        var manifest = new GenerationManifest
        {
            PatientIds = ["p-mixed"],
            Profiles =
            [
                new PatientProfile(new Dictionary<ProfiledMeasureType, MeasureEligibility>
                {
                    [Ach] = MeasureEligibility.NonQualifying,
                    [hypo] = MeasureEligibility.Qualifying,
                })
            ],
            SelectedMeasures = [Ach, hypo],
            ResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["p-mixed"] = new(StringComparer.OrdinalIgnoreCase) { "Encounter/Em", "Condition/Cm" },
            },
        };

        var keys = manifest.GetExpectedAbsKeysForPatient("p-mixed");

        keys.Should().Contain(["Patient/p-mixed", "Encounter/Em", "Condition/Cm"]);
    }

    [Fact]
    public void Simulated_acquired_keys_take_precedence_over_generated_keys_for_qualifying_patient()
    {
        // Sanity check that the existing precedence rule (sim-acquired wins over generated)
        // still holds after the eligibility gate.
        var manifest = new GenerationManifest
        {
            PatientIds = ["p-q"],
            Profiles = [QualifyingProfile()],
            SelectedMeasures = [Ach],
            ResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["p-q"] = new(StringComparer.OrdinalIgnoreCase) { "Encounter/Egen", "Condition/Cgen" },
            },
            SimulatedAcquiredResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["p-q"] = new(StringComparer.OrdinalIgnoreCase) { "Encounter/Esim" },
            },
        };

        var keys = manifest.GetExpectedAbsKeysForPatient("p-q");

        keys.Should().BeEquivalentTo(["Patient/p-q", "Encounter/Esim"]);
    }

    [Fact]
    public void Empty_simulated_acquired_set_does_not_fallback_to_generated_keys()
    {
        var manifest = new GenerationManifest
        {
            PatientIds = ["p-q"],
            Profiles = [QualifyingProfile()],
            SelectedMeasures = [Ach],
            ResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["p-q"] = new(StringComparer.OrdinalIgnoreCase) { "Encounter/Egen", "Condition/Cgen" },
            },
            SimulatedAcquiredResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["p-q"] = new(StringComparer.OrdinalIgnoreCase)
            },
        };

        var keys = manifest.GetExpectedAbsKeysForPatient("p-q");

        keys.Should().BeEquivalentTo(["Patient/p-q"]);
    }

    [Fact]
    public void Expected_abs_counts_do_not_include_organization_when_include_setting_is_false()
    {
        var manifest = new GenerationManifest
        {
            PatientIds = ["p-q"],
            Profiles = [QualifyingProfile()],
            SelectedMeasures = [Ach],
            ResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["p-q"] = new(StringComparer.OrdinalIgnoreCase) { "Encounter/Eq" },
            },
            IncludePatientAggregatorOrganizationResource = false,
        };

        var counts = manifest.GetExpectedAbsCountsForPatient("p-q");

        counts.Should().NotBeNull();
        counts!.Should().NotContainKey("Organization");
    }

    [Fact]
    public void Expected_abs_counts_include_one_organization_when_include_setting_is_true()
    {
        var manifest = new GenerationManifest
        {
            PatientIds = ["p-q"],
            Profiles = [QualifyingProfile()],
            SelectedMeasures = [Ach],
            ResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["p-q"] = new(StringComparer.OrdinalIgnoreCase) { "Encounter/Eq" },
            },
            IncludePatientAggregatorOrganizationResource = true,
        };

        var counts = manifest.GetExpectedAbsCountsForPatient("p-q");

        counts.Should().NotBeNull();
        counts!.Should().ContainKey("Organization");
        counts["Organization"].Should().Be(1);
    }

    [Fact]
    public void Pattern_excluded_patient_is_not_predicted_in_abs_even_if_measure_qualifying()
    {
        var manifest = new GenerationManifest
        {
            PatientIds = ["p-pattern-out"],
            Profiles =
            [
                new PatientProfile(
                    new Dictionary<ProfiledMeasureType, MeasureEligibility> { [Ach] = MeasureEligibility.Qualifying },
                    ScheduledInpatientPattern: ScheduledInpatientPattern.AdmittedAndDischargedAfterPeriod)
            ],
            SelectedMeasures = [Ach],
            ResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["p-pattern-out"] = new(StringComparer.OrdinalIgnoreCase) { "Encounter/E1", "Condition/C1" }
            }
        };

        manifest.GetExpectedAbsKeysForPatient("p-pattern-out").Should().BeEmpty();
        manifest.ExpectedSubmittedPatientIds().Should().BeEmpty();
    }

    [Fact]
    public void Non_qualifying_clinical_shape_excludes_patient_from_predictions()
    {
        var manifest = new GenerationManifest
        {
            PatientIds = ["p-cohort-nq"],
            Profiles =
            [
                new PatientProfile(
                    new Dictionary<ProfiledMeasureType, MeasureEligibility> { [Ach] = MeasureEligibility.NonQualifying })
            ],
            SelectedMeasures = [Ach],
            ResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["p-cohort-nq"] = new(StringComparer.OrdinalIgnoreCase) { "Encounter/E2", "Condition/C2" }
            }
        };

        manifest.GetExpectedAbsKeysForPatient("p-cohort-nq").Should().BeEmpty();
        manifest.ExpectedSubmittedPatientIds().Should().BeEmpty();
    }
}
