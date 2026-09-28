using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Mapping;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Services;

[Trait("Category", "UnitTests")]
public class ResourcesAcquiredTailFinalizerTests
{
    private const string FacilityId = "facility-1";
    private const string CorrelationId = "corr-1";
    private const string PatientId = "Patient/patient-1";
    private static readonly string PatientKey = $"{CorrelationId}:Patient";
    private static readonly string EncounterKey = $"{CorrelationId}:Encounter";

    [Fact]
    public async Task FinalizeAsync_DropsEmptyEncounterKeyAfterStrip()
    {
        var locationMapping = new Mock<ILocationMappingService>();
        locationMapping
            .Setup(s => s.StripNonOrgEncountersFromCacheAsync(FacilityId, CorrelationId, "patient-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stripped(2));

        var cache = new Mock<IResourceCache>();
        cache.Setup(c => c.HasResourcesAsync(PatientKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        cache.Setup(c => c.HasResourcesAsync(EncounterKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new ResourcesAcquiredTailFinalizer(
            locationMapping.Object,
            cache.Object,
            Mock.Of<ILogger<ResourcesAcquiredTailFinalizer>>());

        var tail = BuildTail([PatientKey, EncounterKey]);

        await sut.FinalizeAsync(tail, CancellationToken.None);

        Assert.Equal([PatientKey], tail.ResourcesAcquired.CacheKeys);
        locationMapping.Verify(
            s => s.StripNonOrgEncountersFromCacheAsync(FacilityId, CorrelationId, "patient-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FinalizeAsync_KeepsKeysThatStillHaveResources()
    {
        var locationMapping = new Mock<ILocationMappingService>();
        locationMapping
            .Setup(s => s.StripNonOrgEncountersFromCacheAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stripped(0));

        var cache = new Mock<IResourceCache>();
        cache.Setup(c => c.HasResourcesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new ResourcesAcquiredTailFinalizer(
            locationMapping.Object,
            cache.Object,
            Mock.Of<ILogger<ResourcesAcquiredTailFinalizer>>());

        var tail = BuildTail([PatientKey, EncounterKey]);

        await sut.FinalizeAsync(tail, CancellationToken.None);

        Assert.Equal([PatientKey, EncounterKey], tail.ResourcesAcquired.CacheKeys);
    }

    [Fact]
    public async Task FinalizeAsync_ForgetsCacheTypeWhenNoKeysAreListed()
    {
        var locationMapping = new Mock<ILocationMappingService>();
        locationMapping
            .Setup(s => s.StripNonOrgEncountersFromCacheAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stripped(0));

        var cache = new Mock<IResourceCache>();
        var sut = new ResourcesAcquiredTailFinalizer(
            locationMapping.Object,
            cache.Object,
            Mock.Of<ILogger<ResourcesAcquiredTailFinalizer>>());

        var tail = BuildTail([]);

        await sut.FinalizeAsync(tail, CancellationToken.None);

    }

    [Fact]
    public async Task FinalizeAsync_DoesNotForgetCacheTypeWhenStripThrows()
    {
        var locationMapping = new Mock<ILocationMappingService>();
        locationMapping
            .Setup(s => s.StripNonOrgEncountersFromCacheAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("strip failed"));

        var cache = new Mock<IResourceCache>();
        var sut = new ResourcesAcquiredTailFinalizer(
            locationMapping.Object,
            cache.Object,
            Mock.Of<ILogger<ResourcesAcquiredTailFinalizer>>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.FinalizeAsync(BuildTail([PatientKey]), CancellationToken.None));

    }

    private static TailCompletionResult BuildTail(List<string> cacheKeys) => new()
    {
        FacilityId = FacilityId,
        CorrelationId = CorrelationId,
        PatientId = PatientId,
        ResourcesAcquired = new ResourcesAcquired
        {
            CacheType = ResourceCacheType.ABS,
            CacheKeys = cacheKeys
        }
    };
    /// <summary>
    /// An outcome describing <paramref name="strippedCount"/> non-org encounters removed, one org
    /// encounter kept. The finalizer only forwards this value, so the counts matter to the caller rather
    /// than to the finalizer itself.
    /// </summary>
    private static LocationOrgOutcome Stripped(int strippedCount) =>
        new(LocationOrgStatus.Found, strippedCount + 1, 1, 0, []);
}
