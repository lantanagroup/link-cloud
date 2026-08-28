using Confluent.Kafka;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// Covers how the listener turns a MappingOutcomeEvaluated message into stored indicators: which columns
/// each source is allowed to write, and how the producers' counts resolve to a report-side status.
/// </summary>
[Trait("Category", "UnitTests")]
public class MappingOutcomeListenerTests
{
    private const string FacilityId = "facility-1";
    private const string PatientId = "patient-1";
    private const string HslocSystem = "https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html";
    private const string HslocOid = "urn:oid:2.16.840.1.113883.6.259";
    private const string LocalSystem = "http://hospital.example.org/locations";

    private const string CorrelationId = "correlation-1";

    private static readonly Guid ScheduleId = Guid.NewGuid();

    private readonly Mock<IReportEntryMappingOutcomeManager> _manager = new();
    private readonly MappingOutcomeListener _listener;

    private readonly Mock<IDeadLetterExceptionHandler<MappingOutcomeListener, ResourceKey, string>> _consumeHandler = new();
    private readonly Mock<IDeadLetterExceptionHandler<MappingOutcomeListener, ResourceKey, MappingOutcomeEvaluatedValue>> _deadLetterHandler = new();
    private readonly Mock<ITransientExceptionHandler<MappingOutcomeListener, ResourceKey, MappingOutcomeEvaluatedValue>> _transientHandler = new();

    public MappingOutcomeListenerTests()
    {
        // SetupProperty, not Mock.Of: the constructor assigns Topic, and a plain mock would discard the
        // assignment and make the topic-wiring test pass vacuously.
        _consumeHandler.SetupProperty(item => item.Topic);
        _deadLetterHandler.SetupProperty(item => item.Topic);
        _transientHandler.SetupProperty(item => item.Topic);

        var services = new ServiceCollection();
        services.AddSingleton(_manager.Object);
        var serviceProvider = services.BuildServiceProvider();

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(item => item.ServiceProvider).Returns(serviceProvider);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(item => item.CreateScope()).Returns(scope.Object);

        _listener = new MappingOutcomeListener(
            Mock.Of<ILogger<MappingOutcomeListener>>(),
            Mock.Of<IKafkaConsumerFactory<ResourceKey, MappingOutcomeEvaluatedValue>>(),
            new ServiceInformation { ServiceConfigName = "Report" },
            _consumeHandler.Object,
            _deadLetterHandler.Object,
            _transientHandler.Object,
            Mock.Of<IExceptionLogger<MappingOutcomeListener>>(),
            scopeFactory.Object);
    }

    #region Error handling wiring

    [Fact]
    public void ConstructorWiresTheErrorAndRetryTopics()
    {
        // Both companion topics are declared in topics.txt and MappingOutcomeEvaluatedRetry is registered
        // with Report's RetryScheduleService -- but a handler with no Topic cannot write to either.
        // DeadLetterExceptionHandler.ProduceConsumeExceptionDeadLetter throws on an unset Topic and
        // HandleConsumeException swallows that into a log line, so a poison record would be dropped in
        // silence rather than landing on -Error.
        Assert.Equal("MappingOutcomeEvaluated-Error", _consumeHandler.Object.Topic);
        Assert.Equal("MappingOutcomeEvaluated-Error", _deadLetterHandler.Object.Topic);
        Assert.Equal("MappingOutcomeEvaluated-Retry", _transientHandler.Object.Topic);
    }

    #endregion

    #region Source routing

    [Fact]
    public async Task AcquisitionMessage_WritesOnlyTheAcquisitionColumns()
    {
        await ConsumeAsync(AcquisitionMessage(Outcome(LocationOrgStatus.Found, 10, 3, 0)));

        _manager.Verify(item => item.UpsertAcquisitionOutcomeAsync(
            FacilityId, ScheduleId, PatientId,
            It.IsAny<MappingIndicatorStatus>(), It.IsAny<MappingIndicatorStatus>(),
            It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);

        // Column ownership: an acquisition message must never touch the normalization group, whichever
        // order the two sources arrive in.
        _manager.Verify(item => item.UpsertNormalizationOutcomeAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<IReadOnlyList<CodeMapOutcome>>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NormalizationMessage_WritesOnlyTheNormalizationColumns()
    {
        await ConsumeAsync(NormalizationMessage(CodeMap(HslocSystem, MappingStatus.Mapped, 4, 0)));

        _manager.Verify(item => item.UpsertNormalizationOutcomeAsync(
            FacilityId, ScheduleId, PatientId,
            It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<IReadOnlyList<CodeMapOutcome>>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _manager.Verify(item => item.UpsertAcquisitionOutcomeAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<MappingIndicatorStatus>(), It.IsAny<MappingIndicatorStatus>(),
            It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NormalizationMessage_ForwardsEveryOutcomeUntouched()
    {
        IReadOnlyList<CodeMapOutcome>? captured = null;
        CaptureNormalization(outcomes => captured = outcomes);

        await ConsumeAsync(NormalizationMessage(
            CodeMap(HslocSystem, MappingStatus.Mapped, 1, 0),
            CodeMap(LocalSystem, MappingStatus.Unmapped, 0, 9)));

        // The listener neither filters to HSLOC nor resolves a status. Both belong behind the combination in
        // the manager, because a status derived from one acquisition pass cannot be combined with another's.
        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Count);
        Assert.Contains(captured, outcome => outcome.TargetSystem == LocalSystem);
    }

    [Fact]
    public async Task NormalizationMessage_CarriesThePassIdentityThroughToTheManager()
    {
        string? correlationId = null;
        string? queryType = null;
        CaptureNormalization(pass: (correlation, query) =>
        {
            correlationId = correlation;
            queryType = query;
        });

        await ConsumeAsync(NormalizationMessage(CodeMap(HslocSystem, MappingStatus.Mapped, 1, 0)));

        // Without the identity the manager cannot tell a redelivery from a new pass, and a redelivered
        // message would be counted twice.
        Assert.Equal(CorrelationId, correlationId);
        Assert.Equal("Initial", queryType);
    }

    #endregion

    #region Location Org resolution

    [Theory]
    // No org configuration for the facility, or nothing acquired to evaluate.
    [InlineData(LocationOrgStatus.NotApplicable, 0, 0, 0, MappingIndicatorStatus.NotApplicable)]
    // Encounters existed and none resolved to the organization.
    [InlineData(LocationOrgStatus.NotFound, 5, 0, 0, MappingIndicatorStatus.Unmapped)]
    // Every org encounter got there by the permissive default, so membership was never verified.
    [InlineData(LocationOrgStatus.Found, 3, 3, 3, MappingIndicatorStatus.Assumed)]
    // One genuinely resolved encounter is enough to place the patient in the organization.
    [InlineData(LocationOrgStatus.Found, 5, 3, 2, MappingIndicatorStatus.Mapped)]
    [InlineData(LocationOrgStatus.Found, 5, 3, 0, MappingIndicatorStatus.Mapped)]
    public async Task LocationOrgStatusIsResolvedFromTheCounts(
        LocationOrgStatus producerStatus,
        int encounterCount,
        int orgEncounterCount,
        int assumedOrgEncounterCount,
        MappingIndicatorStatus expected)
    {
        MappingIndicatorStatus? captured = null;
        CaptureAcquisition(locationOrg: status => captured = status);

        await ConsumeAsync(AcquisitionMessage(
            Outcome(producerStatus, encounterCount, orgEncounterCount, assumedOrgEncounterCount)));

        Assert.Equal(expected, captured);
    }

    [Fact]
    public async Task AllOrgEncountersAssumed_IsNotConfusedWithNoneMapped()
    {
        // Both cases satisfy OrgEncounterCount == AssumedOrgEncounterCount when the org count is zero, so
        // the zero case has to be resolved first or the worst result reports as the merely-unverified one.
        MappingIndicatorStatus? captured = null;
        CaptureAcquisition(locationOrg: status => captured = status);

        await ConsumeAsync(AcquisitionMessage(Outcome(LocationOrgStatus.NotFound, 4, 0, 0)));

        Assert.Equal(MappingIndicatorStatus.Unmapped, captured);
    }

    #endregion

    #region Encounter Mapping resolution

    [Theory]
    [InlineData(LocationOrgStatus.NotApplicable, 0, 0, MappingIndicatorStatus.NotApplicable)]
    // Every encounter carried a resolvable location reference.
    [InlineData(LocationOrgStatus.Found, 5, 0, MappingIndicatorStatus.Mapped)]
    // None did.
    [InlineData(LocationOrgStatus.Found, 5, 5, MappingIndicatorStatus.Unmapped)]
    [InlineData(LocationOrgStatus.Found, 5, 2, MappingIndicatorStatus.PartiallyMapped)]
    public async Task EncounterMappingStatusIsResolvedFromTheUnlocatedCount(
        LocationOrgStatus producerStatus,
        int encounterCount,
        int assumedOrgEncounterCount,
        MappingIndicatorStatus expected)
    {
        MappingIndicatorStatus? captured = null;
        CaptureAcquisition(encounterMapping: status => captured = status);

        await ConsumeAsync(AcquisitionMessage(
            Outcome(producerStatus, encounterCount, encounterCount, assumedOrgEncounterCount)));

        Assert.Equal(expected, captured);
    }

    [Fact]
    public async Task EncounterMappingUnmapped_ImpliesLocationOrgAssumed()
    {
        // One-directional, not a biconditional. Assumed is a subset of org membership, so an unmapped
        // encounter column forces every encounter to be both org and unlocated -- which is exactly the
        // assumed case. The converse does not hold: a patient can be all-assumed on their org encounters
        // while other encounters carried locations and resolved elsewhere.
        MappingIndicatorStatus? locationOrg = null;
        MappingIndicatorStatus? encounterMapping = null;
        CaptureAcquisition(
            locationOrg: status => locationOrg = status,
            encounterMapping: status => encounterMapping = status);

        await ConsumeAsync(AcquisitionMessage(Outcome(LocationOrgStatus.Found, 4, 4, 4)));

        Assert.Equal(MappingIndicatorStatus.Unmapped, encounterMapping);
        Assert.Equal(MappingIndicatorStatus.Assumed, locationOrg);
    }

    [Fact]
    public async Task LocationOrgAssumed_DoesNotImplyEncounterMappingUnmapped()
    {
        // The counter-example to the biconditional: two org encounters, both unlocated, alongside three
        // non-org encounters that did carry locations.
        MappingIndicatorStatus? locationOrg = null;
        MappingIndicatorStatus? encounterMapping = null;
        CaptureAcquisition(
            locationOrg: status => locationOrg = status,
            encounterMapping: status => encounterMapping = status);

        await ConsumeAsync(AcquisitionMessage(Outcome(LocationOrgStatus.Found, 5, 2, 2)));

        Assert.Equal(MappingIndicatorStatus.Assumed, locationOrg);
        Assert.Equal(MappingIndicatorStatus.PartiallyMapped, encounterMapping);
    }

    #endregion

    #region Details

    [Fact]
    public async Task AcquisitionDetailsCarryTheCountsAndTheLocationsThatDidNotResolve()
    {
        string? captured = null;
        CaptureAcquisition(details: json => captured = json);

        var outcome = new LocationOrgOutcome(
            LocationOrgStatus.Found, 10, 3, 1,
            [
                new LocationOrgMatch("loc-1", "5 West Medical ICU", "1027-4", "loc-root", true),
                new LocationOrgMatch("loc-2", "Radiology Suite B", "Radiology Suite B", "loc-root", false)
            ]);

        await ConsumeAsync(AcquisitionMessage(outcome));

        Assert.NotNull(captured);
        var details = JsonSerializer.Deserialize<AcquisitionMappingDetails>(captured!);
        Assert.Equal(10, details!.LocationOrg.EncounterCount);
        Assert.Equal(3, details.LocationOrg.OrgEncounterCount);
        Assert.Equal(1, details.LocationOrg.AssumedOrgEncounterCount);

        // The non-resolving location is what a user would act on, so it must survive into storage.
        Assert.Equal(2, details.LocationOrg.Matches.Count);
        Assert.Contains(details.LocationOrg.Matches, match => match.LocationId == "loc-2" && !match.IsOrgLocation);
    }

    #endregion

    #region Message handling

    [Fact]
    public async Task MultipleScheduledReports_RecordTheOutcomeAgainstEach()
    {
        var otherScheduleId = Guid.NewGuid();
        var message = AcquisitionMessage(Outcome(LocationOrgStatus.Found, 1, 1, 0));
        message.Message.Value.ScheduledReports.Add(new ScheduledReport { ReportTrackingId = otherScheduleId.ToString() });

        await ConsumeAsync(message);

        // One acquisition can serve several open reporting periods; the outcome belongs to each of them.
        _manager.Verify(item => item.UpsertAcquisitionOutcomeAsync(
            FacilityId, ScheduleId, PatientId,
            It.IsAny<MappingIndicatorStatus>(), It.IsAny<MappingIndicatorStatus>(),
            It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _manager.Verify(item => item.UpsertAcquisitionOutcomeAsync(
            FacilityId, otherScheduleId, PatientId,
            It.IsAny<MappingIndicatorStatus>(), It.IsAny<MappingIndicatorStatus>(),
            It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NonGuidReportTrackingId_IsSkippedRatherThanThrowing()
    {
        var message = AcquisitionMessage(Outcome(LocationOrgStatus.Found, 1, 1, 0));
        message.Message.Value.ScheduledReports.Add(new ScheduledReport { ReportTrackingId = "not-a-guid" });

        await ConsumeAsync(message);

        _manager.Verify(item => item.UpsertAcquisitionOutcomeAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<MappingIndicatorStatus>(), It.IsAny<MappingIndicatorStatus>(),
            It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PatientIdPrefixIsStrippedBeforeStoring()
    {
        var message = AcquisitionMessage(Outcome(LocationOrgStatus.Found, 1, 1, 0));
        message.Message.Key.PatientId = "Patient/patient-1";

        await ConsumeAsync(message);

        // ReportEntry.PatientId holds the bare id. Storing the reference form would leave every join
        // silently finding nothing, with no error anywhere.
        _manager.Verify(item => item.UpsertAcquisitionOutcomeAsync(
            FacilityId, ScheduleId, "patient-1",
            It.IsAny<MappingIndicatorStatus>(), It.IsAny<MappingIndicatorStatus>(),
            It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("", PatientId)]
    [InlineData(FacilityId, "")]
    public async Task MessageMissingItsKey_DeadLetters(string facilityId, string patientId)
    {
        var message = AcquisitionMessage(Outcome(LocationOrgStatus.Found, 1, 1, 0));
        message.Message.Key.FacilityId = facilityId;
        message.Message.Key.PatientId = patientId;

        // Nothing downstream can place an outcome without both halves of the key.
        //
        // This asserts the contract of ConsumeMessageAsync, which is what the test drives directly. The
        // consume callback is what turns that throw into a dead letter plus a commit; it cannot be reached
        // without the Kafka consumer machinery, so the wiring it depends on is asserted by
        // ConstructorWiresTheErrorAndRetryTopics instead.
        await Assert.ThrowsAsync<DeadLetterException>(() => ConsumeAsync(message));
    }

    #endregion

    #region Helpers

    private void CaptureAcquisition(
        Action<MappingIndicatorStatus>? locationOrg = null,
        Action<MappingIndicatorStatus>? encounterMapping = null,
        Action<string?>? details = null) =>
        _manager
            .Setup(item => item.UpsertAcquisitionOutcomeAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<MappingIndicatorStatus>(), It.IsAny<MappingIndicatorStatus>(),
                It.IsAny<string?>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, string, MappingIndicatorStatus, MappingIndicatorStatus, string?, DateTime, CancellationToken>(
                (_, _, _, locationOrgStatus, encounterMappingStatus, json, _, _) =>
                {
                    locationOrg?.Invoke(locationOrgStatus);
                    encounterMapping?.Invoke(encounterMappingStatus);
                    details?.Invoke(json);
                })
            .Returns(Task.CompletedTask);

    private void CaptureNormalization(
        Action<IReadOnlyList<CodeMapOutcome>>? outcomes = null,
        Action<string?, string?>? pass = null) =>
        _manager
            .Setup(item => item.UpsertNormalizationOutcomeAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<CodeMapOutcome>>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Guid, string, string?, string?, IReadOnlyList<CodeMapOutcome>, DateTime, CancellationToken>(
                (_, _, _, correlationId, queryType, codeMapOutcomes, _, _) =>
                {
                    outcomes?.Invoke(codeMapOutcomes);
                    pass?.Invoke(correlationId, queryType);
                })
            .Returns(Task.CompletedTask);

    private static LocationOrgOutcome Outcome(
        LocationOrgStatus status, int encounterCount, int orgEncounterCount, int assumedOrgEncounterCount) =>
        new(status, encounterCount, orgEncounterCount, assumedOrgEncounterCount, []);

    private static CodeMapOutcome CodeMap(string targetSystem, MappingStatus status, int mapped, int unmapped) =>
        new(LocalSystem, targetSystem, status, mapped, unmapped, 0, []);

    private static ConsumeResult<ResourceKey, MappingOutcomeEvaluatedValue> AcquisitionMessage(
        LocationOrgOutcome locationOrgOutcome) =>
        Message(new MappingOutcomeEvaluatedValue
        {
            Source = MappingOutcomeSource.Acquisition,
            ScheduledReports = [new ScheduledReport { ReportTrackingId = ScheduleId.ToString() }],
            LocationOrgOutcome = locationOrgOutcome
        });

    private static ConsumeResult<ResourceKey, MappingOutcomeEvaluatedValue> NormalizationMessage(
        params CodeMapOutcome[] codeMapOutcomes) =>
        Message(new MappingOutcomeEvaluatedValue
        {
            Source = MappingOutcomeSource.Normalization,
            ScheduledReports = [new ScheduledReport { ReportTrackingId = ScheduleId.ToString() }],
            CodeMapOutcomes = codeMapOutcomes.ToList(),
            CorrelationId = CorrelationId,
            QueryType = "Initial"
        });

    private static ConsumeResult<ResourceKey, MappingOutcomeEvaluatedValue> Message(
        MappingOutcomeEvaluatedValue value) =>
        new()
        {
            Topic = "MappingOutcomeEvaluated",
            Partition = new Partition(0),
            Offset = new Offset(0),
            Message = new Message<ResourceKey, MappingOutcomeEvaluatedValue>
            {
                Key = new ResourceKey { FacilityId = FacilityId, PatientId = PatientId },
                Value = value
            }
        };

    private Task ConsumeAsync(ConsumeResult<ResourceKey, MappingOutcomeEvaluatedValue> message)
    {
        var method = typeof(MappingOutcomeListener)
            .GetMethod("ConsumeMessageAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        return (Task)method.Invoke(_listener, [message, CancellationToken.None])!;
    }

    #endregion
}
