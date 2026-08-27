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
        cache.Setup(c => c.GetAsync(PatientKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Patient { Id = "patient-1" }]);
        cache.Setup(c => c.GetAsync(EncounterKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

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
            .ReturnsAsync(0);

        var cache = new Mock<IResourceCache>();
        cache.Setup(c => c.GetImplementation(ResourceCacheType.ABS)).Returns(cache.Object);
        cache.Setup(c => c.GetAsync(PatientKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Patient { Id = "patient-1" }]);
        cache.Setup(c => c.GetAsync(EncounterKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Encounter { Id = "enc-org" }]);

        var sut = new ResourcesAcquiredTailFinalizer(
            locationMapping.Object,
            cache.Object,
            Mock.Of<ILogger<ResourcesAcquiredTailFinalizer>>());

        var tail = BuildTail([PatientKey, EncounterKey]);

        await sut.FinalizeAsync(tail, CancellationToken.None);

        Assert.Equal([PatientKey, EncounterKey], tail.ResourcesAcquired.CacheKeys);
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
