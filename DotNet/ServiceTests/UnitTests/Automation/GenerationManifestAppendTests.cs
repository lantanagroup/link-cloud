using FluentAssertions;
using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class GenerationManifestAppendTests
{
    private static readonly ProfiledMeasureType Ach =
        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation;

    [Fact]
    public void TryAppendPatient_adds_new_row_and_does_not_rewrite_existing()
    {
        var originalProfile = Qualifying("remain");
        var manifest = new GenerationManifest
        {
            PatientIds = ["remain"],
            Profiles = [originalProfile],
            SelectedMeasures = [Ach],
            ResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["remain"] = new(StringComparer.OrdinalIgnoreCase) { "Encounter/E-remain" }
            }
        };

        var appended = manifest.TryAppendPatient(
            "mid-gen",
            Qualifying("mid-gen"),
            resourceKeys: ["Encounter/E-mid"],
            resourceCountsByType: new Dictionary<string, int> { ["Encounter"] = 1 });

        appended.Should().BeTrue();
        manifest.PatientIds.Should().Equal("remain", "mid-gen");
        manifest.Profiles[0].Should().BeSameAs(originalProfile);
        manifest.ResourceKeysByPatient["remain"].Should().Equal("Encounter/E-remain");
        manifest.ResourceKeysByPatient["mid-gen"].Should().Contain("Encounter/E-mid");
        manifest.ExpectedSubmittedPatientIds().Should().Equal("remain", "mid-gen");

        var rewrite = manifest.TryAppendPatient(
            "remain",
            Qualifying("rewritten"),
            resourceKeys: ["Encounter/E-rewritten"]);

        rewrite.Should().BeFalse();
        manifest.Profiles[0].Should().BeSameAs(originalProfile);
        manifest.ResourceKeysByPatient["remain"].Should().Equal("Encounter/E-remain");
        manifest.PatientIds.Should().Equal("remain", "mid-gen");
    }

    [Fact]
    public void AppendFrom_adds_only_new_patient_rows()
    {
        var original = new GenerationManifest
        {
            PatientIds = ["cohort-1"],
            Profiles = [Qualifying("cohort-1")],
            SelectedMeasures = [Ach],
            ResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["cohort-1"] = new(StringComparer.OrdinalIgnoreCase) { "Patient/cohort-1" }
            }
        };

        var slice = new GenerationManifest
        {
            PatientIds = ["cohort-1", "upload-1"],
            Profiles = [Qualifying("should-not-replace"), Qualifying("upload-1")],
            SelectedMeasures = [Ach],
            ResourceKeysByPatient = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["cohort-1"] = new(StringComparer.OrdinalIgnoreCase) { "Patient/replaced" },
                ["upload-1"] = new(StringComparer.OrdinalIgnoreCase) { "Patient/upload-1" }
            },
            PreExistingPatientIds = new HashSet<string>(StringComparer.Ordinal) { "upload-1" }
        };

        original.AppendFrom(slice).Should().Be(1);
        original.PatientIds.Should().Equal("cohort-1", "upload-1");
        original.ResourceKeysByPatient["cohort-1"].Should().Equal("Patient/cohort-1");
        original.PreExistingPatientIds.Should().Contain("upload-1");
        original.ExpectedSubmittedPatientIds().Should().Equal("cohort-1", "upload-1");
    }

    [Fact]
    public void IncrementalBuilder_records_template_cache_key_on_snapshot()
    {
        var builder = new GenerationManifest.IncrementalBuilder();
        builder.AddPatient("Patient-1", Qualifying("Patient-1"));
        builder.SetTemplateCacheKey("Patient-1", "template-key-abc");

        var snapshot = builder.Build([Ach]).ToSnapshot();

        snapshot.TemplateCacheKeyByPatient.Should().ContainKey("Patient-1");
        snapshot.TemplateCacheKeyByPatient["Patient-1"].Should().Be("template-key-abc");
    }

    [Fact]
    public void AppendFrom_copies_template_cache_keys()
    {
        var original = new GenerationManifest
        {
            PatientIds = ["cohort-1"],
            Profiles = [Qualifying("cohort-1")],
            SelectedMeasures = [Ach],
            TemplateCacheKeyByPatient = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["cohort-1"] = "key-original"
            }
        };

        var slice = new GenerationManifest
        {
            PatientIds = ["live-1"],
            Profiles = [Qualifying("live-1")],
            SelectedMeasures = [Ach],
            TemplateCacheKeyByPatient = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["live-1"] = "key-live"
            }
        };

        original.AppendFrom(slice).Should().Be(1);
        original.TemplateCacheKeyByPatient["cohort-1"].Should().Be("key-original");
        original.TemplateCacheKeyByPatient["live-1"].Should().Be("key-live");
        original.ToSnapshot().TemplateCacheKeyByPatient["live-1"].Should().Be("key-live");
    }

    [Fact]
    public void TryAppendPatient_non_qualifying_import_is_tracked_but_not_submitted()
    {
        var manifest = new GenerationManifest
        {
            PatientIds = ["remain"],
            Profiles = [Qualifying("remain")],
            SelectedMeasures = [Ach]
        };

        manifest.TryAppendPatient("outside", NonQualifying()).Should().BeTrue();
        manifest.PatientIds.Should().Equal("remain", "outside");
        manifest.ExpectedSubmittedPatientIds().Should().Equal("remain");
    }

    private static PatientProfile Qualifying(string _)
        => new(
            new Dictionary<ProfiledMeasureType, MeasureEligibility> { [Ach] = MeasureEligibility.Qualifying },
            ScheduledInpatientPattern: ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod);

    private static PatientProfile NonQualifying()
        => new(
            new Dictionary<ProfiledMeasureType, MeasureEligibility> { [Ach] = MeasureEligibility.NonQualifying },
            ScheduledInpatientPattern: ScheduledInpatientPattern.AdmittedAndDischargedAfterPeriod,
            CohortQualification: MeasureEligibility.NonQualifying);
}
