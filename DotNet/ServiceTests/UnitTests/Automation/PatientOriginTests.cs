using System.Text.Json;
using FluentAssertions;
using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class PatientOriginTests
{
    [Fact]
    public void Resolve_classifies_generated_existing_id_and_bundle_patients()
    {
        var origins = PatientOrigin.Resolve(
            ["gen-1", " ", "existing-1", "gen-1", "bundle-1"],
            ["existing-1"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["gen-1"] = "template-key",
                ["bundle-1"] = " "
            });

        origins.Should().HaveCount(3);
        origins["gen-1"].Should().Be(PatientOrigin.Generated);
        origins["existing-1"].Should().Be(PatientOrigin.Uploaded);
        origins["bundle-1"].Should().Be(PatientOrigin.Uploaded);
    }

    [Fact]
    public void Resolve_treats_a_pre_existing_patient_as_uploaded_even_with_a_template_key()
    {
        var origins = PatientOrigin.Resolve(
            ["existing-1"],
            ["existing-1"],
            new Dictionary<string, string> { ["existing-1"] = "template-key" });

        origins["existing-1"].Should().Be(PatientOrigin.Uploaded);
    }

    [Fact]
    public void ToSnapshot_records_origins_and_confirms_the_generator_suite()
    {
        var suiteId = Guid.NewGuid();
        var mapId = Guid.NewGuid();
        var manifest = new GenerationManifest
        {
            PatientIds = ["gen-1", "existing-1", "bundle-1"],
            TemplateCacheKeyByPatient = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["gen-1"] = "template-key"
            },
            PreExistingPatientIds = new HashSet<string>(StringComparer.Ordinal) { "existing-1" },
            AppliedNormalizationSuiteId = suiteId,
            AppliedNormalizationSuiteName = "Epic",
            NormalizationSuiteAppliedToGenerator = true,
            AppliedOrganizationResourceMapTemplateId = mapId,
            AppliedOrganizationResourceMapName = "Epic map"
        };

        var snapshot = manifest.ToSnapshot();

        snapshot.PatientOriginByPatient.Should().NotBeNull();
        snapshot.PatientOriginByPatient!["gen-1"].Should().Be(PatientOrigin.Generated);
        snapshot.PatientOriginByPatient["existing-1"].Should().Be(PatientOrigin.Uploaded);
        snapshot.PatientOriginByPatient["bundle-1"].Should().Be(PatientOrigin.Uploaded);
        snapshot.GeneratedWithSelectedSuite.Should().BeTrue();
        snapshot.AppliedNormalizationSuiteId.Should().Be(suiteId);
        snapshot.AppliedNormalizationSuiteName.Should().Be("Epic");
        snapshot.AppliedOrganizationResourceMapTemplateId.Should().Be(mapId);
        snapshot.AppliedOrganizationResourceMapName.Should().Be("Epic map");
    }

    [Fact]
    public void ToSnapshot_does_not_confirm_the_generator_when_no_patient_was_generated()
    {
        var snapshot = new GenerationManifest
        {
            PatientIds = ["existing-1"],
            PreExistingPatientIds = new HashSet<string>(StringComparer.Ordinal) { "existing-1" },
            AppliedNormalizationSuiteName = "Epic",
            NormalizationSuiteAppliedToGenerator = true
        }.ToSnapshot();

        snapshot.GeneratedWithSelectedSuite.Should().BeFalse();
        snapshot.PatientOriginByPatient!["existing-1"].Should().Be(PatientOrigin.Uploaded);
    }

    [Fact]
    public void Older_snapshot_json_leaves_origin_unset()
    {
        var snapshot = JsonSerializer.Deserialize<GenerationManifestSnapshot>(
            """{"PatientCount":1,"PatientIds":["p1"]}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        snapshot.Should().NotBeNull();
        snapshot!.PatientOriginByPatient.Should().BeNull();
        snapshot.GeneratedWithSelectedSuite.Should().BeFalse();
        snapshot.AppliedNormalizationSuiteId.Should().BeNull();
        snapshot.AppliedOrganizationResourceMapTemplateId.Should().BeNull();
    }
}
