
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Mapping;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Report.Listeners;

public class MappingOutcomeListener(
    ILogger<MappingOutcomeListener> logger,
    IKafkaConsumerFactory<ResourceKey, MappingOutcomeEvaluatedValue> consumerFactory,
    ServiceInformation serviceInformation,
    IDeadLetterExceptionHandler<MappingOutcomeListener, ResourceKey, string> consumeExceptionHandler,
    IServiceScopeFactory serviceScopeFactory
    ) : BackgroundService
{
    private readonly ILogger<MappingOutcomeListener> _logger = logger;
    private readonly IKafkaConsumerFactory<ResourceKey, MappingOutcomeEvaluatedValue> _consumerFactory = consumerFactory;
    private readonly ServiceInformation _serviceInformation = serviceInformation;
    private readonly IDeadLetterExceptionHandler<MappingOutcomeListener, ResourceKey, string> _consumeExceptionHandler = consumeExceptionHandler;
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private bool _cancelled = false;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await Task.Run(() => StartConsumerLoop(cancellationToken), cancellationToken);
    }

    private async Task StartConsumerLoop(CancellationToken cancellationToken)
    {
        using var kafkaConsumer = _consumerFactory.CreateConsumer(new ConsumerConfig
        {
            GroupId = _serviceInformation.ServiceConfigName,
            EnableAutoCommit = false
        });

        kafkaConsumer.Subscribe(new string[] { KafkaTopic.MappingOutcomeEvaluated.ToString() });

        while (!cancellationToken.IsCancellationRequested && !_cancelled)
        {
            try
            {
                await kafkaConsumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                {
                    try
                    {
                        await ConsumeMessageAsync(result, consumeCancellationToken);
                    }
                    finally
                    {
                        if (!consumeCancellationToken.IsCancellationRequested)
                            kafkaConsumer.Commit(result);
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ConsumeException ex)
            {
                if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                {
                    throw new OperationCanceledException(ex.Error.Reason, ex);
                }

                string facilityId = string.Empty;
                if (ex.ConsumerRecord?.Message?.Key != null)
                {
                    try
                    {
                        var key = JsonSerializer.Deserialize<ResourceKey>(ex.ConsumerRecord.Message.Key);
                        facilityId = key?.FacilityId ?? string.Empty;
                    }
                    catch
                    {
                        // ignore
                    }
                }

                _consumeExceptionHandler.HandleConsumeException(ex, facilityId);
                var offset = ex.ConsumerRecord?.TopicPartitionOffset;

                if (offset == null)
                {
                    kafkaConsumer.Commit();
                }
                else
                {
                    kafkaConsumer.Commit(new List<TopicPartitionOffset>
                    {
                        offset
                    });
                }
            }
        }
    }

    private async Task ConsumeMessageAsync(
        ConsumeResult<ResourceKey,MappingOutcomeEvaluatedValue> result, 
        CancellationToken consumeCancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var reportEntryMappingOutcomeManager = scope.ServiceProvider.GetRequiredService<IReportEntryMappingOutcomeManager>();

        var value = result.Message.Value;
        var facilityId = result.Message.Key.FacilityId;
        var patientId = result.Message.Key.PatientId.SplitReference();

        if (string.IsNullOrWhiteSpace(facilityId) || string.IsNullOrWhiteSpace(patientId) || value is null)
        {
            throw new DeadLetterException("Invalid MappingOutcomeEvaluated message");
        }

        var scheduleIds = value.ScheduledReports
            .Select(sr => sr.ReportTrackingId)
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse!)
            .Distinct()
            .ToList();

        var now = DateTime.UtcNow;

        foreach (var scheduleId in scheduleIds)
        {
            await RecordOutcomeAsync(
                reportEntryMappingOutcomeManager, value, facilityId, scheduleId, patientId, now, consumeCancellationToken);
        }
    }

    /// <summary>
    /// Writes only the columns this message's source owns, so the two producers cannot overwrite each
    /// other whichever order they arrive in.
    /// </summary>
    private static Task RecordOutcomeAsync(
        IReportEntryMappingOutcomeManager outcomeManager,
        MappingOutcomeEvaluatedValue value,
        string facilityId,
        Guid scheduleId,
        string patientId,
        DateTime evaluatedAt,
        CancellationToken cancellationToken) =>
        value.Source switch
        {
            MappingOutcomeSource.Acquisition => outcomeManager.UpsertAcquisitionOutcomeAsync(
                facilityId,
                scheduleId,
                patientId,
                MapLocationOrg(value.LocationOrgOutcome),
                MapEncounterMapping(value.LocationOrgOutcome),
                SerializeAcquisitionDetails(value.LocationOrgOutcome),
                evaluatedAt,
                cancellationToken),

            MappingOutcomeSource.Normalization => outcomeManager.UpsertNormalizationOutcomeAsync(
                facilityId,
                scheduleId,
                patientId,
                MapCodeMap(value.CodeMapOutcomes.Where(outcome => MappingTargetSystems.IsHsloc(outcome.TargetSystem))),
                SerializeNormalizationDetails(value.CodeMapOutcomes),
                evaluatedAt,
                cancellationToken),

            _ => throw new DeadLetterException($"Unknown mapping outcome source: {value.Source}")
        };

    /// <summary>
    /// Resolves an indicator from every outcome reported against one target system.
    /// </summary>
    /// <remarks>
    /// Takes a sequence rather than a single outcome because a facility may map several source systems into
    /// the same target, producing one outcome per source. Their counts sum and the status is projected from
    /// the totals. An empty sequence is <see cref="MappingIndicatorStatus.NotApplicable"/>: the message was
    /// authoritative and reported nothing for that target system.
    /// </remarks>
    private static MappingIndicatorStatus MapCodeMap(IEnumerable<CodeMapOutcome> outcomes)
    {
        var reported = false;
        var mappedCount = 0;
        var unmappedCount = 0;
        var failureCount = 0;

        foreach (var outcome in outcomes)
        {
            reported = true;
            mappedCount += outcome.MappedCount;
            unmappedCount += outcome.UnmappedCount;
            failureCount += outcome.FailureCount;
        }

        // No outcome at all for this target system: nothing was configured to write it.
        if (!reported)
        {
            return MappingIndicatorStatus.NotApplicable;
        }

        // Outcomes exist but nothing was counted. Either the code maps ran and had nothing to act on, or
        // every one of them failed -- and a processing fault must not be reported as a configuration gap.
        if (mappedCount == 0 && unmappedCount == 0)
        {
            return failureCount > 0
                ? MappingIndicatorStatus.Unknown
                : MappingIndicatorStatus.NotApplicable;
        }

        if (unmappedCount == 0)
        {
            return MappingIndicatorStatus.Mapped;
        }

        if (mappedCount == 0)
        {
            return MappingIndicatorStatus.Unmapped;
        }

        return MappingIndicatorStatus.PartiallyMapped;
    }

    private static string? SerializeAcquisitionDetails(LocationOrgOutcome? locationOrgOutcome) =>
        locationOrgOutcome is null
            ? null
            : JsonSerializer.Serialize(new AcquisitionMappingDetails(
                new LocationOrgDetails(
                    locationOrgOutcome.Status,
                    locationOrgOutcome.EncounterCount,
                    locationOrgOutcome.OrgEncounterCount,
                    locationOrgOutcome.AssumedOrgEncounterCount,
                    locationOrgOutcome.Matches)));

    // Every outcome is retained, including target systems no column recognizes -- without them a facility
    // with a mistyped system is invisible outside the logs.
    private static string SerializeNormalizationDetails(IReadOnlyList<CodeMapOutcome> codeMapOutcomes) =>
        JsonSerializer.Serialize(new NormalizationMappingDetails(codeMapOutcomes));

    /// <summary>
    /// Resolves the Encounter Mapping indicator: were the patient's encounters mapped to locations at all?
    /// </summary>
    /// <remarks>
    /// <para>
    /// A different question from Location Org, off the same counts. Location Org asks whether the patient
    /// belongs to the reporting organization; this asks whether their encounters carried locations that
    /// could be resolved in the first place. An encounter with no resolvable location reference is counted
    /// as unlocated, and acquisition reports exactly those as assumed-org — so
    /// <c>AssumedOrgEncounterCount</c> is the unlocated count and the located count is the remainder.
    /// </para>
    /// <para>
    /// That equivalence holds for anything acquisition produced, because the branch that leaves an encounter
    /// without location rows is the same branch that marks it as belonging to the organization. It would not
    /// hold for an encounter mapping edited through the DataAcquisition API, which can set org membership
    /// and location rows independently.
    /// </para>
    /// </remarks>
    /// <param name="locationOrgOutcome">
    /// The acquisition outcome. Null only if an Acquisition-sourced message arrived without one, which the
    /// contract does not produce.
    /// </param>
    private static MappingIndicatorStatus MapEncounterMapping(LocationOrgOutcome? locationOrgOutcome)
    {
        if (locationOrgOutcome is null)
        {
            return MappingIndicatorStatus.NotEvaluated;
        }

        // Nothing to locate, or the facility does not resolve locations to an organization at all.
        if (locationOrgOutcome.Status == LocationOrgStatus.NotApplicable || locationOrgOutcome.EncounterCount == 0)
        {
            return MappingIndicatorStatus.NotApplicable;
        }

        // Every encounter carried at least one resolvable location reference.
        if (locationOrgOutcome.AssumedOrgEncounterCount == 0)
        {
            return MappingIndicatorStatus.Mapped;
        }

        // No encounter did. The patient is in the report on encounters whose location is unknown.
        if (locationOrgOutcome.AssumedOrgEncounterCount == locationOrgOutcome.EncounterCount)
        {
            return MappingIndicatorStatus.Unmapped;
        }

        return MappingIndicatorStatus.PartiallyMapped;
    }

    /// <summary>
    /// Resolves the Location Org indicator: did this patient resolve to the reporting organization?
    /// </summary>
    /// <remarks>
    /// <para>
    /// The producer reports a coarse status plus the encounter counts behind it; the finer
    /// <see cref="MappingIndicatorStatus.Assumed"/> distinction is derived here rather than on the wire, so
    /// recognizing it required no change to the message contract.
    /// </para>
    /// <para>
    /// Order matters. <c>OrgEncounterCount == 0</c> must be tested before the assumed comparison, or a
    /// patient with no org encounters at all would satisfy <c>0 == 0</c> and report as assumed rather than
    /// unmapped — inverting the result on exactly the patient the indicator exists to flag.
    /// </para>
    /// </remarks>
    /// <param name="locationOrgOutcome">
    /// The acquisition outcome. Null only if an Acquisition-sourced message arrived without one, which the
    /// contract does not produce.
    /// </param>
    private static MappingIndicatorStatus MapLocationOrg(LocationOrgOutcome? locationOrgOutcome)
    {
        if (locationOrgOutcome is null)
        {
            return MappingIndicatorStatus.NotEvaluated;
        }

        // NotApplicable from the producer means org-location mapping is not active for the facility, or the
        // correlation acquired nothing to evaluate. Neither is a failure to resolve.
        if (locationOrgOutcome.Status == LocationOrgStatus.NotApplicable || locationOrgOutcome.EncounterCount == 0)
        {
            return MappingIndicatorStatus.NotApplicable;
        }

        // Encounters existed and none belonged to the organization. Their resources were stripped from the
        // bundle, so this patient is reported on data the organization does not own.
        if (locationOrgOutcome.OrgEncounterCount == 0)
        {
            return MappingIndicatorStatus.Unmapped;
        }

        // Every org encounter got there by the permissive default — no resolvable location references — so
        // membership was never checked against the facility's configuration. Assumed is a subset of org
        // membership, which is why equality is the test rather than a zero org count.
        if (locationOrgOutcome.OrgEncounterCount == locationOrgOutcome.AssumedOrgEncounterCount)
        {
            return MappingIndicatorStatus.Assumed;
        }

        // A patient with at least one genuinely resolved encounter is demonstrably in the organization. A
        // mix stays Mapped, with the assumed count available in the detail for drill-down.
        return MappingIndicatorStatus.Mapped;
    }
}

