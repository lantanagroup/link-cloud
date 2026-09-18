using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Models;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class GeneratedPatientBundleReplayServiceTests
{
    [Fact]
    public void FileNameFor_sanitizes_unsafe_characters()
    {
        GeneratedPatientBundleReplayService.FileNameFor("Patient/ab:01")
            .Should().Be("generated-bundle-Patient-ab-01.json");
        GeneratedPatientBundleReplayService.FileNameFor("Patient-ok_1")
            .Should().Be("generated-bundle-Patient-ok_1.json");
    }

    [Fact]
    public async Task ReplayAsync_materializes_cached_template_for_manifest_patient()
    {
        var cache = TemplateCacheReturning("key-a");
        var versions = LatestVersion(2);
        var sut = new GeneratedPatientBundleReplayService(cache.Object, versions.Object);

        var result = await sut.ReplayAsync(
            RunWithCacheVersion(2, "scenario:abc"),
            ManifestWithKey("Patient-abcd1234-001", "key-a"),
            "Patient-abcd1234-001");

        result.Found.Should().BeTrue();
        result.BundleJson.Should().Contain("Patient-abcd1234-001");
        result.BundleJson.Should().NotContain("template-run");
        result.GenerationChanged.Should().BeFalse();
        result.RunCacheVersion.Should().Be(2);
        result.LatestCacheVersion.Should().Be(2);
        result.FileName.Should().Be("generated-bundle-Patient-abcd1234-001.json");
    }

    [Fact]
    public async Task ReplayAsync_unknown_patient_is_not_found()
    {
        var sut = new GeneratedPatientBundleReplayService(
            new Mock<IGeneratedPatientTemplateCache>(MockBehavior.Strict).Object,
            LatestVersion(null).Object);

        var result = await sut.ReplayAsync(
            new AutomationRunSummary(),
            new GenerationManifestSnapshot { PatientIds = ["Patient-1"] },
            "Patient-other");

        result.Found.Should().BeFalse();
        result.Error.Should().Contain("manifest");
    }

    [Fact]
    public async Task ReplayAsync_imported_patient_without_template_key_is_unavailable()
    {
        var sut = new GeneratedPatientBundleReplayService(
            new Mock<IGeneratedPatientTemplateCache>(MockBehavior.Strict).Object,
            LatestVersion(null).Object);

        var result = await sut.ReplayAsync(
            new AutomationRunSummary(),
            new GenerationManifestSnapshot { PatientIds = ["import-1"] },
            "import-1");

        result.Found.Should().BeFalse();
        result.Error.Should().Contain("no generation template");
    }

    [Fact]
    public async Task ReplayAsync_flags_stale_generation_when_newer_version_exists()
    {
        var cache = TemplateCacheReturning("key-v1");
        var versions = LatestVersion(4);
        var sut = new GeneratedPatientBundleReplayService(cache.Object, versions.Object);

        var result = await sut.ReplayAsync(
            RunWithCacheVersion(1, "scenario:stale"),
            ManifestWithKey("Patient-abcd1234-001", "key-v1"),
            "Patient-abcd1234-001");

        result.Found.Should().BeTrue();
        result.GenerationChanged.Should().BeTrue();
        result.RunCacheVersion.Should().Be(1);
        result.LatestCacheVersion.Should().Be(4);
    }

    [Fact]
    public async Task ReplayAsync_missing_template_blob_is_unavailable()
    {
        var cache = new Mock<IGeneratedPatientTemplateCache>(MockBehavior.Strict);
        cache.Setup(c => c.GetAsync("key-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeneratedPatientTemplate?)null);

        var sut = new GeneratedPatientBundleReplayService(cache.Object, LatestVersion(1).Object);
        var result = await sut.ReplayAsync(
            RunWithCacheVersion(1, "scenario:abc"),
            ManifestWithKey("Patient-abcd1234-001", "key-a"),
            "Patient-abcd1234-001");

        result.Found.Should().BeFalse();
        result.Error.Should().Contain("no longer in cache");
    }

    private static Mock<IGeneratedPatientTemplateCache> TemplateCacheReturning(string key)
    {
        var cache = new Mock<IGeneratedPatientTemplateCache>(MockBehavior.Strict);
        cache.Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedPatientTemplate(
                "template-run",
                ["""{"resourceType":"Bundle","type":"transaction","entry":[{"resource":{"resourceType":"Patient","id":"Patient-template-run-001"}}]}"""]));
        return cache;
    }

    private static Mock<IGeneratedTemplateCacheVersionLookup> LatestVersion(int? versionNumber)
    {
        var versions = new Mock<IGeneratedTemplateCacheVersionLookup>(MockBehavior.Strict);
        versions.Setup(v => v.GetLatestAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(versionNumber is int n
                ? new GeneratedTemplateCacheVersionBinding(Guid.NewGuid(), n, "scenario:abc", "hash")
                : null);
        return versions;
    }

    private static AutomationRunSummary RunWithCacheVersion(int version, string scenarioKey)
        => new()
        {
            GeneratedTemplateCacheVersionNumber = version,
            GeneratedTemplateCacheScenarioKey = scenarioKey
        };

    private static GenerationManifestSnapshot ManifestWithKey(string patientId, string templateKey)
        => new()
        {
            PatientIds = [patientId],
            TemplateCacheKeyByPatient = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [patientId] = templateKey
            }
        };
}
