using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.AcquisitionWorker.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Services;

/// <summary>
/// Covers the tail produce path: the MappingOutcomeEvaluated message that carries the org-location result
/// alongside the ResourcesAcquired message that drives the pipeline.
/// </summary>
[Trait("Category", "UnitTests")]
public class AcquisitionProcessorBackgroundServiceTests
{
    private const string FacilityId = "facility-1";
    private const string CorrelationId = "corr-1";
    private const long LogId = 42;

    private readonly Mock<IProducer<ResourceKey, ResourcesAcquired>> _mockResourceAcquiredProducer = new();
    private readonly Mock<IProducer<ResourceKey, MappingOutcomeEvaluatedValue>> _mockMappingOutcomeProducer = new();
    private readonly Mock<IResourcesAcquiredTailFinalizer> _mockTailFinalizer = new();
    private readonly Mock<IDataAcquisitionLogManager> _mockLogManager = new();
    private readonly AcquisitionProcessorBackgroundService _service;
    private readonly IServiceProvider _scopeProvider;

    public AcquisitionProcessorBackgroundServiceTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_mockTailFinalizer.Object);
        _scopeProvider = services.BuildServiceProvider();

        _service = new AcquisitionProcessorBackgroundService(
            Mock.Of<ILogger<AcquisitionProcessorBackgroundService>>(),
            Mock.Of<IServiceProvider>(),
            _mockResourceAcquiredProducer.Object,
            _mockMappingOutcomeProducer.Object);
    }

    [Fact]
    public async Task TryProduceTailMessage_ProducesMappingOutcomeCarryingTheLocationOrgResult()
    {
        // Arrange
        var outcome = new LocationOrgOutcome(
            LocationOrgStatus.Found,
            EncounterCount: 10,
            OrgEncounterCount: 3,
            AssumedOrgEncounterCount: 1,
            Matches: [new LocationOrgMatch("loc-a", "5 West Medical ICU", "1027-4", "loc-root", true)]);

        var scheduledReports = new List<ScheduledReport> { new() { ReportTrackingId = Guid.NewGuid().ToString() } };

        SetupTail(scheduledReports);
        SetupStrip(outcome);

        Message<ResourceKey, MappingOutcomeEvaluatedValue>? produced = null;
        CaptureMappingOutcome(message => produced = message);

        // Act
        await InvokeTryProduceTailMessageAsync();

        // Assert — the message identifies its producer, so an empty CodeMapOutcomes list is unambiguous
        // downstream, and carries the outcome unmodified.
        Assert.NotNull(produced);
        Assert.Equal(MappingOutcomeSource.Acquisition, produced!.Value.Source);
        Assert.Same(outcome, produced.Value.LocationOrgOutcome);
        Assert.Empty(produced.Value.CodeMapOutcomes);

        // The schedules come from the tail, so one acquisition fans out to every report it served.
        Assert.Equal(scheduledReports, produced.Value.ScheduledReports);
        Assert.Equal(FacilityId, produced.Key.FacilityId);
    }

    [Fact]
    public async Task TryProduceTailMessage_StillProducesResourcesAcquired_WhenMappingOutcomeProduceFails()
    {
        // Arrange — the outcome producer is broken.
        SetupTail();
        SetupStrip(NotApplicable());

        _mockMappingOutcomeProducer
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ResourceKey, MappingOutcomeEvaluatedValue>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KafkaException(ErrorCode.Local_MsgTimedOut));

        // Act
        await InvokeTryProduceTailMessageAsync();

        // Assert — the indicator is reporting metadata; the pipeline is the product. Losing the outcome
        // must never cost the correlation its ResourcesAcquired message.
        _mockResourceAcquiredProducer.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ResourcesAcquired.ToString(),
                It.IsAny<Message<ResourceKey, ResourcesAcquired>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryProduceTailMessage_ProducesMappingOutcomeBeforeResourcesAcquired()
    {
        // Arrange
        SetupTail();
        SetupStrip(NotApplicable());

        var order = new List<string>();
        _mockMappingOutcomeProducer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<ResourceKey, MappingOutcomeEvaluatedValue>>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add(nameof(MappingOutcomeEvaluatedValue)))
            .ReturnsAsync(new DeliveryResult<ResourceKey, MappingOutcomeEvaluatedValue>());
        _mockResourceAcquiredProducer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<ResourceKey, ResourcesAcquired>>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add(nameof(ResourcesAcquired)))
            .ReturnsAsync(new DeliveryResult<ResourceKey, ResourcesAcquired>());

        // Act
        await InvokeTryProduceTailMessageAsync();

        // Assert — the outcome is produced from state that exists before the tail is announced, so it goes
        // out first and its failure path stays ahead of the pipeline message.
        Assert.Equal([nameof(MappingOutcomeEvaluatedValue), nameof(ResourcesAcquired)], order);
    }

    [Fact]
    public async Task TryProduceTailMessage_GroupNotComplete_ProducesNothing()
    {
        // Arrange — TryCompleteTailAsync returns null until every sibling log is terminal.
        _mockLogManager
            .Setup(m => m.TryCompleteTailAsync(LogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TailCompletionResult?)null);

        // Act
        await InvokeTryProduceTailMessageAsync();

        // Assert — a partial group must not report an org-location outcome for encounters still arriving.
        _mockMappingOutcomeProducer.Verify(
            p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<ResourceKey, MappingOutcomeEvaluatedValue>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockResourceAcquiredProducer.Verify(
            p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<ResourceKey, ResourcesAcquired>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockTailFinalizer.Verify(
            s => s.FinalizeAsync(It.IsAny<TailCompletionResult>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryProduceTailMessage_SupplementalPhase_ProducesResourcesAcquiredButNoMappingOutcome()
    {
        // Arrange — the second pass over a reportable patient. Encounters are an INITIAL-phase query, so
        // nothing re-acquires them; the cache still holds what the INITIAL strip left behind.
        SetupTail(queryType: nameof(QueryPhase.Supplemental));
        SetupStrip(NotApplicable());

        // Act
        await InvokeTryProduceTailMessageAsync();

        // Assert — reporting from this pass would describe only the encounters that survived the strip,
        // and it arrives last, so it would overwrite the real result with a count of survivors over
        // survivors. The pipeline message is unaffected.
        _mockMappingOutcomeProducer.Verify(
            p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<ResourceKey, MappingOutcomeEvaluatedValue>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockResourceAcquiredProducer.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ResourcesAcquired.ToString(),
                It.IsAny<Message<ResourceKey, ResourcesAcquired>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryProduceTailMessage_SupplementalPhase_StillStripsTheCache()
    {
        // Arrange
        SetupTail(queryType: nameof(QueryPhase.Supplemental));
        SetupStrip(NotApplicable());

        // Act
        await InvokeTryProduceTailMessageAsync();

        // Assert — the gate covers reporting only. Stripping stays unconditional: it is what guarantees no
        // non-org encounter reaches MeasureEval, and it must not become contingent on a reporting concern.
        _mockTailFinalizer.Verify(
            s => s.FinalizeAsync(
                It.Is<TailCompletionResult>(t => t.FacilityId == FacilityId && t.CorrelationId == CorrelationId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Polling")]
    [InlineData("Monitoring")]
    public async Task TryProduceTailMessage_NonInitialPhase_ProducesNoMappingOutcome(string queryType)
    {
        // Arrange — a phase the org-location outcome says nothing about. ToWireQueryType passes unexpected
        // phases through as their raw name rather than rewriting them, so they reach here verbatim.
        SetupTail(queryType: queryType);
        SetupStrip(NotApplicable());

        // Act
        await InvokeTryProduceTailMessageAsync();

        // Assert — the gate is a whitelist, not a Supplemental blacklist, so an unrecognized phase reports
        // nothing rather than attributing whatever the cache happened to hold to the patient's mapping.
        _mockMappingOutcomeProducer.Verify(
            p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<ResourceKey, MappingOutcomeEvaluatedValue>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryProduceTailMessage_InitialPhaseCasingVaries_StillProducesMappingOutcome()
    {
        // Arrange — the phase arrives as a string built from the enum name, so casing is not guaranteed by
        // the type system the way the enum itself would be.
        SetupTail(queryType: "INITIAL");
        SetupStrip(NotApplicable());
        CaptureMappingOutcome(_ => { });

        // Act
        await InvokeTryProduceTailMessageAsync();

        // Assert
        _mockMappingOutcomeProducer.Verify(
            p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<ResourceKey, MappingOutcomeEvaluatedValue>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryProduceTailMessage_StripsResourceTypePrefixFromPatientIdBeforeLookup()
    {
        // Arrange — the tail carries the patient id in reference form.
        SetupTail(patientId: "Patient/abc-123");
        SetupStrip(NotApplicable());

        // Act
        await InvokeTryProduceTailMessageAsync();

        // Assert — EncounterMapping.PatientId holds the bare id, so the prefix must be stripped or every
        // lookup silently misses and the outcome reports zeros without erroring. That normalisation now
        // happens inside the finalizer, so what the worker owes is the tail with the prefixed id intact;
        // ResourcesAcquiredTailFinalizerTests covers the stripping itself.
        _mockTailFinalizer.Verify(
            s => s.FinalizeAsync(
                It.Is<TailCompletionResult>(t => t.PatientId == "Patient/abc-123"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static LocationOrgOutcome NotApplicable() =>
        new(LocationOrgStatus.NotApplicable, 0, 0, 0, []);

    private void SetupTail(
        List<ScheduledReport>? scheduledReports = null,
        string patientId = "patient-1",
        string queryType = nameof(QueryPhase.Initial))
    {
        _mockLogManager
            .Setup(m => m.TryCompleteTailAsync(LogId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TailCompletionResult
            {
                FacilityId = FacilityId,
                CorrelationId = CorrelationId,
                PatientId = patientId,
                ResourcesAcquired = new ResourcesAcquired
                {
                    QueryType = queryType,
                    ScheduledReports = scheduledReports ?? []
                }
            });

        _mockResourceAcquiredProducer
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<ResourceKey, ResourcesAcquired>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<ResourceKey, ResourcesAcquired>());
    }

    private void SetupStrip(LocationOrgOutcome outcome) =>
        _mockTailFinalizer
            .Setup(s => s.FinalizeAsync(It.IsAny<TailCompletionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);

    private void CaptureMappingOutcome(Action<Message<ResourceKey, MappingOutcomeEvaluatedValue>> capture) =>
        _mockMappingOutcomeProducer
            .Setup(p => p.ProduceAsync(
                KafkaTopic.MappingOutcomeEvaluated.ToString(),
                It.IsAny<Message<ResourceKey, MappingOutcomeEvaluatedValue>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<ResourceKey, MappingOutcomeEvaluatedValue>, CancellationToken>((_, message, _) => capture(message))
            .ReturnsAsync(new DeliveryResult<ResourceKey, MappingOutcomeEvaluatedValue>());

    private Task InvokeTryProduceTailMessageAsync()
    {
        var method = typeof(AcquisitionProcessorBackgroundService)
            .GetMethod("TryProduceTailMessageAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        return (Task)method.Invoke(
            _service,
            [_scopeProvider, _mockLogManager.Object, LogId, CancellationToken.None])!;
    }
}
