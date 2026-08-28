
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Mapping;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Report.Listeners;

public class MappingOutcomeListener : BackgroundService
{
    private readonly ILogger<MappingOutcomeListener> _logger;
    private readonly IKafkaConsumerFactory<ResourceKey, MappingOutcomeEvaluatedValue> _consumerFactory;
    private readonly ServiceInformation _serviceInformation;
    private readonly IDeadLetterExceptionHandler<MappingOutcomeListener, ResourceKey, string> _consumeExceptionHandler;
    private readonly IDeadLetterExceptionHandler<MappingOutcomeListener, ResourceKey, MappingOutcomeEvaluatedValue> _deadLetterExceptionHandler;
    private readonly ITransientExceptionHandler<MappingOutcomeListener, ResourceKey, MappingOutcomeEvaluatedValue> _transientExceptionHandler;
    private readonly IExceptionLogger<MappingOutcomeListener> _exceptionLogger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private bool _cancelled = false;

    public MappingOutcomeListener(
        ILogger<MappingOutcomeListener> logger,
        IKafkaConsumerFactory<ResourceKey, MappingOutcomeEvaluatedValue> consumerFactory,
        ServiceInformation serviceInformation,
        IDeadLetterExceptionHandler<MappingOutcomeListener, ResourceKey, string> consumeExceptionHandler,
        IDeadLetterExceptionHandler<MappingOutcomeListener, ResourceKey, MappingOutcomeEvaluatedValue> deadLetterExceptionHandler,
        ITransientExceptionHandler<MappingOutcomeListener, ResourceKey, MappingOutcomeEvaluatedValue> transientExceptionHandler,
        IExceptionLogger<MappingOutcomeListener> exceptionLogger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _consumerFactory = consumerFactory ?? throw new ArgumentNullException(nameof(consumerFactory));
        _serviceInformation = serviceInformation ?? throw new ArgumentNullException(nameof(serviceInformation));
        _consumeExceptionHandler = consumeExceptionHandler ?? throw new ArgumentNullException(nameof(consumeExceptionHandler));
        _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentNullException(nameof(deadLetterExceptionHandler));
        _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentNullException(nameof(transientExceptionHandler));
        _exceptionLogger = exceptionLogger ?? throw new ArgumentNullException(nameof(exceptionLogger));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));

        // Without these the companion topics exist and nothing can write to them:
        // DeadLetterExceptionHandler.ProduceConsumeExceptionDeadLetter throws on an unset Topic and
        // HandleConsumeException swallows that into a log line, so a poison record would be dropped
        // silently rather than landing on -Error.
        _consumeExceptionHandler.Topic = nameof(KafkaTopic.MappingOutcomeEvaluated) + "-Error";
        _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.MappingOutcomeEvaluated) + "-Error";
        _transientExceptionHandler.Topic = nameof(KafkaTopic.MappingOutcomeEvaluated) + "-Retry";
    }

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
                    if (result is null)
                    {
                        throw new DeadLetterException(
                            $"Received null message from topic '{nameof(KafkaTopic.MappingOutcomeEvaluated)}'.");
                    }

                    var facilityId = result.Message.Key?.FacilityId ?? string.Empty;

                    // Every branch below commits. An exception that escaped here would reach ExecuteAsync,
                    // and .NET's default BackgroundServiceExceptionBehavior of StopHost would take the
                    // whole Report service down -- every listener, the API and the Quartz jobs -- over one
                    // unparseable mapping outcome.
                    try
                    {
                        await ConsumeMessageAsync(result, consumeCancellationToken);
                        kafkaConsumer.SafeCommit(result, _logger);
                    }
                    catch (DeadLetterException ex)
                    {
                        _deadLetterExceptionHandler.HandleException(result, ex, facilityId);
                        kafkaConsumer.SafeCommit(result, _logger);
                    }
                    catch (TransientException ex)
                    {
                        _transientExceptionHandler.HandleException(result, ex, facilityId);
                        kafkaConsumer.SafeCommit(result, _logger);
                    }
                    catch (TimeoutException ex)
                    {
                        var transientException = new TransientException(
                            $"Timeout encountered at offset {result.TopicPartitionOffset}.", ex);

                        _transientExceptionHandler.HandleException(result, transientException, facilityId);
                        kafkaConsumer.SafeCommit(result, _logger);
                    }
                    catch (OperationCanceledException) when (consumeCancellationToken.IsCancellationRequested)
                    {
                        // Shutdown, not a message fault. Left uncommitted deliberately so the message is
                        // redelivered rather than skipped; the write it performs is idempotent.
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // Anything unclassified is treated as transient: a DbUpdateConcurrencyException
                        // from the outcome upsert, or a transient SQL fault, should be retried rather than
                        // discarded or allowed to stop the service.
                        _transientExceptionHandler.HandleException(result, ex, facilityId);
                        kafkaConsumer.SafeCommit(result, _logger);
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

                kafkaConsumer.SafeCommit(
                    offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset },
                    _logger);
            }
            catch (Exception ex)
            {
                // The backstop. The callback above handles per-message faults, but a failure in the commit
                // itself, in the consume machinery, or in the ConsumeException handling would otherwise
                // escape to ExecuteAsync and stop the host. Logging and continuing keeps the other six
                // Report listeners, the API and the Quartz jobs running.
                _exceptionLogger.Handle(ex, $"Error encountered in {nameof(MappingOutcomeListener)}", LogLevel.Error);
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
                value.CorrelationId,
                value.QueryType,
                value.CodeMapOutcomes,
                evaluatedAt,
                cancellationToken),

            _ => throw new DeadLetterException($"Unknown mapping outcome source: {value.Source}")
        };

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

