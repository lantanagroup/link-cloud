using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.Shared.Application.Enums;
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
            .ReturnsAsync(2);

        var cache = new Mock<IResourceCache>();
        cache.Setup(c => c.GetImplementation(ResourceCacheType.ABS)).Returns(cache.Object);
        cache.Setup(c => c.GetImplementation(ResourceCacheType.Redis))
            .Throws(new NotSupportedException());
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
        cache.Verify(c => c.ForgetCacheTypeForCorrelationId(CorrelationId), Times.Once);
    }

    [Fact]
    public async Task FinalizeAsync_KeepsKeysThatStillHaveResources()
    {
        var locationMapping = new Mock<ILocationMappingService>();
        locationMapping
            .Setup(s => s.StripNonOrgEncountersFromCacheAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var cache = new Mock<IResourceCache>();
        cache.Setup(c => c.GetImplementation(ResourceCacheType.ABS)).Returns(cache.Object);
        cache.Setup(c => c.GetImplementation(ResourceCacheType.Redis))
            .Throws(new NotSupportedException());
        cache.Setup(c => c.HasResourcesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new ResourcesAcquiredTailFinalizer(
            locationMapping.Object,
            cache.Object,
            Mock.Of<ILogger<ResourcesAcquiredTailFinalizer>>());

        var tail = BuildTail([PatientKey, EncounterKey]);

        await sut.FinalizeAsync(tail, CancellationToken.None);

        Assert.Equal([PatientKey, EncounterKey], tail.ResourcesAcquired.CacheKeys);
        cache.Verify(c => c.ForgetCacheTypeForCorrelationId(CorrelationId), Times.Once);
    }

    [Fact]
    public async Task FinalizeAsync_ForgetsCacheTypeWhenNoKeysAreListed()
    {
        var locationMapping = new Mock<ILocationMappingService>();
        locationMapping
            .Setup(s => s.StripNonOrgEncountersFromCacheAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var cache = new Mock<IResourceCache>();
        var sut = new ResourcesAcquiredTailFinalizer(
            locationMapping.Object,
            cache.Object,
            Mock.Of<ILogger<ResourcesAcquiredTailFinalizer>>());

        var tail = BuildTail([]);

        await sut.FinalizeAsync(tail, CancellationToken.None);

        cache.Verify(c => c.ForgetCacheTypeForCorrelationId(CorrelationId), Times.Once);
        cache.Verify(c => c.GetImplementation(It.IsAny<ResourceCacheType>()), Times.Never);
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

        cache.Verify(c => c.ForgetCacheTypeForCorrelationId(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task FinalizeAsync_KeepsAbsKeysWhenStampedCacheTypeWasRedis()
    {
        var locationMapping = new Mock<ILocationMappingService>();
        locationMapping
            .Setup(s => s.StripNonOrgEncountersFromCacheAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var redis = new Mock<IResourceCache>();
        redis.Setup(c => c.HasResourcesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var abs = new Mock<IResourceCache>();
        abs.Setup(c => c.HasResourcesAsync(PatientKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        abs.Setup(c => c.HasResourcesAsync(EncounterKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cache = new Mock<IResourceCache>();
        cache.Setup(c => c.GetImplementation(ResourceCacheType.Redis)).Returns(redis.Object);
        cache.Setup(c => c.GetImplementation(ResourceCacheType.ABS)).Returns(abs.Object);

        var sut = new ResourcesAcquiredTailFinalizer(
            locationMapping.Object,
            cache.Object,
            Mock.Of<ILogger<ResourcesAcquiredTailFinalizer>>());

        var tail = BuildTail([PatientKey, EncounterKey]);
        tail.ResourcesAcquired.CacheType = ResourceCacheType.Redis;

        await sut.FinalizeAsync(tail, CancellationToken.None);

        Assert.Equal([PatientKey, EncounterKey], tail.ResourcesAcquired.CacheKeys);
        Assert.Equal(ResourceCacheType.ABS, tail.ResourcesAcquired.CacheType);
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
}
