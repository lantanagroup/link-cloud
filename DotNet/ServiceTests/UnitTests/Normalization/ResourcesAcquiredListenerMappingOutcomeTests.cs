using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Listeners;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using System.Text.Json;
using FhirResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

/// <summary>
/// Covers the MappingOutcomeEvaluated message the listener produces alongside ResourcesNormalized,
/// carrying the code map results accumulated across the correlation.
/// </summary>
[Trait("Category", "UnitTests")]
public class ResourcesAcquiredListenerMappingOutcomeTests
{
    private const string FacilityId = "facility-1";
    private const string PatientId = "patient-1";
    private const string CorrelationId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";
    private const string LocalSystem = "http://hospital.example.org/locations";
    private const string HslocSystem = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";

    private static readonly string LocationCacheKey = $"{CorrelationId}:Location";

    [Fact]
    public async Task ProcessMessageAsync_ProducesOutcomeIdentifyingNormalizationAsTheSource()
    {
        var outcomeProducer = new Mock<IProducer<ResourceKey, MappingOutcomeValue>>();
        var listener = BuildListener(
            LocationWithTypeCodes("ICU", "PHARMACY"),
            CodeMapSequence(("ICU", "1027-4")),
            outcomeProducer);

        Message<ResourceKey, MappingOutcomeValue>? produced = null;
        Capture(outcomeProducer, message => produced = message);

        await listener.ProcessMessageAsync(BuildConsumeResult(), CancellationToken.None);

        Assert.NotNull(produced);

        // Source is what lets the consumer read an empty CodeMapOutcomes as a real result rather than as
        // an absence, and it selects which stored columns this message is allowed to write.
        Assert.Equal(MappingOutcomeSource.Normalization, produced!.Value.Source);

        // Location org resolution belongs to DataAcquisition; Normalization neither computes nor forwards it.
        Assert.Null(produced.Value.LocationOrgOutcome);

        Assert.Equal(FacilityId, produced.Key.FacilityId);
        Assert.Equal(PatientId, produced.Key.PatientId);
    }

    [Fact]
    public async Task ProcessMessageAsync_CarriesTheCodeMapCountsAndUnmappedCodes()
    {
        var outcomeProducer = new Mock<IProducer<ResourceKey, MappingOutcomeValue>>();
        var listener = BuildListener(
            LocationWithTypeCodes("ICU", "PHARMACY", "LAB"),
            CodeMapSequence(("ICU", "1027-4")),
            outcomeProducer);

        Message<ResourceKey, MappingOutcomeValue>? produced = null;
        Capture(outcomeProducer, message => produced = message);

        await listener.ProcessMessageAsync(BuildConsumeResult(), CancellationToken.None);

        var outcome = Assert.Single(produced!.Value.CodeMapOutcomes);
        Assert.Equal(LocalSystem, outcome.SourceSystem);
        Assert.Equal(HslocSystem, outcome.TargetSystem);
        Assert.Equal(MappingStatus.PartiallyMapped, outcome.Status);
        Assert.Equal(1, outcome.MappedCount);
        Assert.Equal(2, outcome.UnmappedCount);
        Assert.Equal(["LAB", "PHARMACY"], outcome.UnmappedCodes.OrderBy(code => code));
    }

    [Fact]
    public async Task ProcessMessageAsync_ForwardsTheScheduledReportsFromTheAcquiredMessage()
    {
        var outcomeProducer = new Mock<IProducer<ResourceKey, MappingOutcomeValue>>();
        var listener = BuildListener(
            LocationWithTypeCodes("ICU"),
            CodeMapSequence(("ICU", "1027-4")),
            outcomeProducer);

        Message<ResourceKey, MappingOutcomeValue>? produced = null;
        Capture(outcomeProducer, message => produced = message);

        await listener.ProcessMessageAsync(BuildConsumeResult(), CancellationToken.None);

        // One correlation can serve several open reporting periods, and the outcome has to reach each of
        // them. Losing these would leave the message with nowhere to be stored.
        var scheduledReport = Assert.Single(produced!.Value.ScheduledReports);
        Assert.Equal("tracking-1", scheduledReport.ReportTrackingId);
    }

    [Fact]
    public async Task ProcessMessageAsync_NoCodeMapsConfigured_StillProducesAnEmptyOutcome()
    {
        var outcomeProducer = new Mock<IProducer<ResourceKey, MappingOutcomeValue>>();
        var listener = BuildListener(
            LocationWithTypeCodes("ICU"),
            sequences: [],
            outcomeProducer);

        Message<ResourceKey, MappingOutcomeValue>? produced = null;
        Capture(outcomeProducer, message => produced = message);

        await listener.ProcessMessageAsync(BuildConsumeResult(), CancellationToken.None);

        // An empty list from Normalization is authoritative: the facility configured no code maps. Staying
        // silent instead would leave the indicator indistinguishable from a patient still in flight.
        Assert.NotNull(produced);
        Assert.Empty(produced!.Value.CodeMapOutcomes);
    }

    [Fact]
    public async Task ProcessMessageAsync_ProducesResourcesNormalizedAsWell()
    {
        var outcomeProducer = new Mock<IProducer<ResourceKey, MappingOutcomeValue>>();
        var normalizedProducer = new Mock<IProducer<ResourceKey, ResourcesNormalizedValue>>();
        var listener = BuildListener(
            LocationWithTypeCodes("ICU"),
            CodeMapSequence(("ICU", "1027-4")),
            outcomeProducer,
            normalizedProducer);

        Capture(outcomeProducer, _ => { });

        await listener.ProcessMessageAsync(BuildConsumeResult(), CancellationToken.None);

        // The outcome rides its own topic; ResourcesNormalized is untouched by it.
        normalizedProducer.Verify(
            item => item.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ResourceKey, ResourcesNormalizedValue>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        outcomeProducer.Verify(
            item => item.ProduceAsync(
                KafkaTopic.MappingOutcomeEvaluated.ToString(),
                It.IsAny<Message<ResourceKey, MappingOutcomeValue>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_CountsSpanEveryResourceInTheCorrelation()
    {
        var outcomeProducer = new Mock<IProducer<ResourceKey, MappingOutcomeValue>>();
        var listener = BuildListener(
            [
                LocationWithTypeCodes("ICU").Single(),
                LocationWithTypeCodes("PHARMACY", "ICU").Single()
            ],
            CodeMapSequence(("ICU", "1027-4")),
            outcomeProducer);

        Message<ResourceKey, MappingOutcomeValue>? produced = null;
        Capture(outcomeProducer, message => produced = message);

        await listener.ProcessMessageAsync(BuildConsumeResult(), CancellationToken.None);

        // The accumulator is scoped to the message, not to a resource, so the outcome is per patient.
        var outcome = Assert.Single(produced!.Value.CodeMapOutcomes);
        Assert.Equal(2, outcome.MappedCount);
        Assert.Equal(1, outcome.UnmappedCount);
    }

    [Fact]
    public async Task ProcessMessageAsync_CarriesTheCorrelationIdHeader()
    {
        var outcomeProducer = new Mock<IProducer<ResourceKey, MappingOutcomeValue>>();
        var listener = BuildListener(
            LocationWithTypeCodes("ICU"),
            CodeMapSequence(("ICU", "1027-4")),
            outcomeProducer);

        Message<ResourceKey, MappingOutcomeValue>? produced = null;
        Capture(outcomeProducer, message => produced = message);

        await listener.ProcessMessageAsync(BuildConsumeResult(), CancellationToken.None);

        var header = Assert.Single(produced!.Headers, item => item.Key == NormalizationConstants.HeaderNames.CorrelationId);
        Assert.Equal(CorrelationId, Encoding.UTF8.GetString(header.GetValueBytes()));
    }

    [Fact]
    public async Task ProcessMessageAsync_OutcomeProduceFails_DoesNotFailTheMessageOrSkipCacheCleanup()
    {
        var outcomeProducer = new Mock<IProducer<ResourceKey, MappingOutcomeValue>>();
        outcomeProducer
            .Setup(item => item.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ResourceKey, MappingOutcomeValue>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KafkaException(ErrorCode.Local_MsgTimedOut));

        var resourceCache = new Mock<IResourceCache>();
        var listener = BuildListener(
            LocationWithTypeCodes("ICU"),
            CodeMapSequence(("ICU", "1027-4")),
            outcomeProducer,
            resourceCache: resourceCache);

        // The indicator is reporting metadata; the pipeline is the product. ResourcesNormalized has already
        // been produced by this point, so letting the exception escape would redeliver the message and
        // produce it a second time -- a worse outcome than losing the indicator.
        await listener.ProcessMessageAsync(BuildConsumeResult(), CancellationToken.None);

        // Cleanup follows the produce, so a throw would also have stranded the copied cache keys.
        resourceCache.Verify(
            item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    private static void Capture(
        Mock<IProducer<ResourceKey, MappingOutcomeValue>> producer,
        Action<Message<ResourceKey, MappingOutcomeValue>> capture) =>
        producer
            .Setup(item => item.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ResourceKey, MappingOutcomeValue>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<ResourceKey, MappingOutcomeValue>, CancellationToken>((_, message, _) => capture(message))
            .ReturnsAsync(new DeliveryResult<ResourceKey, MappingOutcomeValue>());

    private static List<DomainResource> LocationWithTypeCodes(params string[] codes)
    {
        var location = new Location { Id = "loc-1" };
        foreach (var code in codes)
        {
            location.Type.Add(new CodeableConcept { Coding = { new Coding(LocalSystem, code) } });
        }

        return [location];
    }

    private static List<OperationSequenceModel> CodeMapSequence(params (string Source, string Target)[] codes)
    {
        var operation = new CodeMapOperation(
            "Location type to HSLOC",
            "Location.type.coding",
            [
                new CodeSystemMap(LocalSystem, HslocSystem, codes.ToDictionary(
                    pair => pair.Source,
                    pair => new CodeMap(pair.Target, $"Display for {pair.Target}")))
            ]);

        return
        [
            new OperationSequenceModel
            {
                Sequence = 1,
                FacilityId = FacilityId,
                OperationResourceType = new OperationResourceTypeModel
                {
                    Operation = new OperationModel
                    {
                        Name = operation.Name,
                        OperationType = OperationType.CodeMap.ToString(),
                        OperationJson = JsonSerializer.Serialize(operation)
                    }
                }
            }
        ];
    }

    private static ResourcesAcquiredListener BuildListener(
        List<DomainResource> resources,
        List<OperationSequenceModel> sequences,
        Mock<IProducer<ResourceKey, MappingOutcomeValue>> mappingOutcomeProducer,
        Mock<IProducer<ResourceKey, ResourcesNormalizedValue>>? normalizedProducer = null,
        Mock<IResourceCache>? resourceCache = null)
    {
        normalizedProducer ??= new Mock<IProducer<ResourceKey, ResourcesNormalizedValue>>();
        resourceCache ??= new Mock<IResourceCache>();

        resourceCache
            .Setup(item => item.GetImplementation(It.IsAny<ResourceCacheType>()))
            .Returns(resourceCache.Object);
        resourceCache
            .Setup(item => item.GetResourceTypeByCacheKey(LocationCacheKey))
            .Returns(FhirResourceType.Location);
        resourceCache
            .Setup(item => item.GetAsync(LocationCacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resources);

        var sequenceQueries = new Mock<IOperationSequenceQueries>();
        sequenceQueries
            .Setup(item => item.Search(
                It.IsAny<OperationSequenceSearchModel>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sequences);

        var services = new ServiceCollection();
        services.AddSingleton(sequenceQueries.Object);
        var serviceProvider = services.BuildServiceProvider();

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(item => item.ServiceProvider).Returns(serviceProvider);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(item => item.CreateScope()).Returns(scope.Object);

        var deadLetterHandler = new Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>();
        deadLetterHandler.SetupProperty(item => item.Topic);
        var transientHandler = new Mock<ITransientExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>();
        transientHandler.SetupProperty(item => item.Topic);
        var consumeExceptionHandler = new Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, string>>();
        consumeExceptionHandler.SetupProperty(item => item.Topic);

        return new ResourcesAcquiredListener(
            Mock.Of<ILogger<ResourcesAcquiredListener>>(),
            new ServiceInformation { ServiceConfigName = "Normalization" },
            scopeFactory.Object,
            Mock.Of<IKafkaConsumerFactory<ResourceKey, ResourcesAcquiredValue>>(),
            consumeExceptionHandler.Object,
            deadLetterHandler.Object,
            transientHandler.Object,
            Mock.Of<INormalizationServiceMetrics>(),
            normalizedProducer.Object,
            new CopyPropertyOperationService(Mock.Of<ILogger<CopyPropertyOperationService>>()),
            new CodeMapOperationService(Mock.Of<ILogger<CodeMapOperationService>>()),
            new ConditionalTransformOperationService(Mock.Of<ILogger<ConditionalTransformOperationService>>()),
            new CopyLocationOperationService(Mock.Of<ILogger<CopyLocationOperationService>>()),
            new CopyLocationAliasToTypeIterativelyOperationService(Mock.Of<ILogger<CopyLocationAliasToTypeIterativelyOperationService>>()),
            new RemoveExtensionsOperationService(Mock.Of<ILogger<RemoveExtensionsOperationService>>()),
            resourceCache.Object,
            Mock.Of<IResourceCachePurger>(),
            mappingOutcomeProducer.Object);
    }

    private static ConsumeResult<ResourceKey, ResourcesAcquiredValue> BuildConsumeResult()
    {
        var headers = new Headers
        {
            new Header(NormalizationConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(CorrelationId))
        };

        return new ConsumeResult<ResourceKey, ResourcesAcquiredValue>
        {
            Topic = "ResourcesAcquired",
            Partition = new Partition(0),
            Offset = new Offset(0),
            Message = new Message<ResourceKey, ResourcesAcquiredValue>
            {
                Headers = headers,
                Key = new ResourceKey { FacilityId = FacilityId, PatientId = PatientId },
                Value = new ResourcesAcquiredValue
                {
                    QueryType = "Initial",
                    ReportableEvent = "Adhoc",
                    ScheduledReports = [new ScheduledReport { ReportTrackingId = "tracking-1" }],
                    CacheType = ResourceCacheType.ABS,
                    CacheKeys = [LocationCacheKey]
                }
            }
        };
    }
}
