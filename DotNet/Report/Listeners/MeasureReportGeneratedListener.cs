using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Services.ResourceMerger;
using LantanaGroup.Link.Report.Services.ResourceMerger.Strategies;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Settings;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;
using static Hl7.Fhir.Model.MeasureReport;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Listeners
{
    public class MeasureReportGeneratedListener : BackgroundService
    {
        private readonly ILogger<MeasureReportGeneratedListener> _logger;
        private readonly IKafkaConsumerFactory<Null, MeasureReportGeneratedValue> _kafkaConsumerFactory;

        private readonly IServiceScopeFactory _serviceScopeFactory;

        private readonly ITransientExceptionHandler<Null, MeasureReportGeneratedValue> _transientExceptionHandler;
        private readonly IDeadLetterExceptionHandler<Null, MeasureReportGeneratedValue> _deadLetterExceptionHandler;

        private readonly BlobStorageService _blobStorageService;
        private readonly ReadyForValidationProducer _readyForValidationProducer;
        private readonly AuditableEventOccurredProducer _auditableEventOccurredProducer;
        private readonly ServiceInformation _serviceInformation;

        private string Name => this.GetType().Name;

        public MeasureReportGeneratedListener(
            ILogger<MeasureReportGeneratedListener> logger,
            IKafkaConsumerFactory<Null, MeasureReportGeneratedValue> kafkaConsumerFactory,
            ITransientExceptionHandler<Null, MeasureReportGeneratedValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<Null, MeasureReportGeneratedValue> deadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory,
            ServiceInformation serviceInformation,
            BlobStorageService blobStorageService,
            ReadyForValidationProducer readyForValidationProducer,
            AuditableEventOccurredProducer auditableEventOccurredProducer)
        {

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));

            _serviceScopeFactory = serviceScopeFactory;

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.Topic = nameof(KafkaTopic.MeasureReportGenerated) + "-Retry";
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.MeasureReportGenerated) + "-Error";

            _blobStorageService = blobStorageService;
            _readyForValidationProducer = readyForValidationProducer;
            _auditableEventOccurredProducer = auditableEventOccurredProducer;
            _serviceInformation = serviceInformation;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async Task StartConsumerLoop(CancellationToken cancellationToken)
        {
            var consumerConfig = new ConsumerConfig()
            {
                GroupId = _serviceInformation.ServiceConfigName,
                EnableAutoCommit = false
            };

            using var consumer = _kafkaConsumerFactory.CreateConsumer(consumerConfig);
            try
            {
                consumer.Subscribe(nameof(KafkaTopic.MeasureReportGenerated));
                _logger.LogInformation("Started MeasureReportGenerated consumer on {date} for topic '{MeasureReportGeneratedName}'", DateTime.UtcNow, nameof(KafkaTopic.MeasureReportGenerated));

                while (!cancellationToken.IsCancellationRequested)
                {
                    string facilityId = string.Empty;

                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            try
                            {
                                facilityId = result.Message.Value.FacilityId;
                                await ProcessMessageAsync(result, facilityId, cancellationToken);
                            }
                            catch (DeadLetterException ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, ex, facilityId);
                            }
                            catch (TransientException ex)
                            {
                                _transientExceptionHandler.HandleException(result, ex, facilityId);
                            }
                            catch (Exception ex)
                            {
                                _deadLetterExceptionHandler.HandleException(result, new DeadLetterException("Report - MeasureReportGenerated Exception thrown: " + ex.Message), facilityId);
                            }
                            finally
                            {
                                consumer.Commit(result);
                            }
                        }, cancellationToken);
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming message for topics: [{Topics}] at {Timestamp}", string.Join(", ", consumer.Subscription), DateTime.UtcNow);

                        if (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                        {
                            throw new OperationCanceledException(ex.Error.Reason, ex);
                        }

                        _deadLetterExceptionHandler.HandleConsumeException(ex, facilityId);

                        var offset = ex.ConsumerRecord?.TopicPartitionOffset;
                        consumer.Commit(offset == null ? new List<TopicPartitionOffset>() : new List<TopicPartitionOffset> { offset });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error encountered in MeasureReportGeneratedListener");
                        consumer.Commit();
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger.LogError(oce, "Operation Canceled: {Message}", oce.Message);
                consumer.Close();
                consumer.Dispose();
            }
        }

        public async Task ProcessMessageAsync(ConsumeResult<Null, MeasureReportGeneratedValue> result, string facilityId, CancellationToken cancellationToken)
        {

            if (result.Message.Value == null)
            {
                throw new DeadLetterException($"{Name}: MeasureReportGenerated event value segment missing");
            }

            if (!result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
            {
                throw new DeadLetterException($"{Name}: Received message without correlation ID (ReportId = {result.Message.Value.ReportTrackingId}, FacilityId = {result.Message.Value.FacilityId}).");
            }

            var messageValue = result.Message.Value;

            _logger.LogDebug("Consuming MeasureReportGenerated (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId}, ReportType = {ReportType})", messageValue.FacilityId, messageValue.PatientId, messageValue.ReportTrackingId, messageValue.ReportType);

            using var scope = _serviceScopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
            var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();
            var reportResourceManager = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();
            var reportPopulationManager = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();
            var patientAggregator = scope.ServiceProvider.GetRequiredService<PatientAggregator>();
            var reportManifestProducer = scope.ServiceProvider.GetRequiredService<ReportManifestProducer>();

            var correlationId = Encoding.UTF8.GetString(headerValue);

            var schedule = await reportScheduledManager.GetReportSchedule(messageValue.FacilityId, messageValue.ReportTrackingId, cancellationToken);

            if (schedule == null)
            {
                throw new DeadLetterException($"{Name}: No scheduled report record was found (ReportId = {messageValue.ReportTrackingId}, FacilityId = {result.Message.Value.FacilityId}).");
            }

            var reportEntry = await reportEntryManager.UpdateAsyncWithConsumerResult(messageValue);

            var isAllNonReportable = reportEntry.MeasureReportList.All(x => x.Status == Domain.Enums.MeasureReportStatus.NotReportable);

            //Handles the case when all Measure Reports for a patient do not meet the criteria for the reported measures. In this case, we want to update the patient entry as 'Not Reportable'. Afterwards, we will attempt to produce a manifest if this consumed event was the last for the reporting period.
            if (isAllNonReportable)
            {
                _logger.LogDebug("Entry not reportable (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", messageValue.FacilityId, messageValue.PatientId, messageValue.ReportTrackingId);

                await reportEntryManager.UpdateAsyncNotReportableEntry(reportEntry, cancellationToken);
                await reportManifestProducer.Produce(schedule, correlationId);
                return;
            }

            var readyForAggregation = reportEntry.MeasureReportList.All(x => x.Status == Domain.Enums.MeasureReportStatus.NotReportable || x.Status == Domain.Enums.MeasureReportStatus.ReadyForValidation);

            //The aggregation step for a patient will only be performed once the Report service has consumed 'MeasureReportGenerated' events for all entries in reportEntry.MeasureReportList.
            if (!readyForAggregation)
            {
                _logger.LogDebug("Patient not ready for aggregation (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", messageValue.FacilityId, messageValue.PatientId, messageValue.ReportTrackingId);
                //TODO - Daniel: The 'isAllNonReportable' logic above was recently added (2/12/2026) and may replace the need for executing reportManifestProducer below. It won't hurt to run, but may not be needed. If we find that we don't need to execute the manifest producer, we will only need to return in this block.
                await reportManifestProducer.Produce(schedule, correlationId);
                return;
            }

            _logger.LogDebug("Patient ready for aggregation (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", messageValue.FacilityId, messageValue.PatientId, messageValue.ReportTrackingId);
            var startTime = Stopwatch.GetTimestamp();

            AggregateResult aggregateResult;

            try
            {
                aggregateResult = await patientAggregator.AggregateToABS(messageValue.PatientId, schedule);
            }
            catch (Exception ex)
            {
                throw new DeadLetterException(ex.Message);
            }

            var elapsed = Stopwatch.GetElapsedTime(startTime);

            _logger.LogDebug("Patient aggregation complete. Elapsed time: {Elapsed} (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", elapsed.ToString(), messageValue.FacilityId, messageValue.PatientId, messageValue.ReportTrackingId);

            startTime = Stopwatch.GetTimestamp();

            await reportEntryManager.UpdateAsyncWithAggregateResult(reportEntry, aggregateResult);
            await reportResourceManager.AddAsyncWithAggregateResult(facilityId, messageValue.ReportTrackingId, messageValue.PatientId, aggregateResult, cancellationToken);

            foreach (var aggregateMeasureReport in aggregateResult.MeasureReportResults)
            {
                var populationModel = await reportPopulationManager.SingleOrDefaultAsync(x => x.ReportScheduleId == messageValue.ReportTrackingId && x.ReportType == messageValue.ReportType);

                if (populationModel == null)
                {
                    await reportPopulationManager.AddAsyncWithAggregateResult(messageValue.FacilityId, messageValue.ReportTrackingId, aggregateMeasureReport, cancellationToken);
                    continue;
                }

                await reportPopulationManager.UpdateAsyncWithAggregateResult(populationModel, aggregateMeasureReport, cancellationToken);
            }

            if (reportEntry.MeasureReportList.All(x => x.Status == Domain.Enums.MeasureReportStatus.NotReportable))
            {
                await reportEntryManager.UpdateAsyncNotReportableEntry(reportEntry, cancellationToken);
                return;
            }

            elapsed = Stopwatch.GetElapsedTime(startTime);

            _logger.LogDebug("Update to Mongo collections complete. Elapsed time: {Elapsed} (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", elapsed.ToString(), messageValue.FacilityId, messageValue.PatientId, messageValue.ReportTrackingId);

            try
            {
                await _readyForValidationProducer.Produce(schedule.Id, schedule.ReportTypes, schedule.FacilityId, messageValue.PatientId, aggregateResult.Uri.AbsoluteUri, correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error was encountered producing a ReadyForValidation (ReportId = {reportId}, FacilityId = {facilityId}, PatientId = {patientId}).", schedule.Id.SanitizeUntrustedString(), schedule.FacilityId.SanitizeUntrustedString(), messageValue.PatientId.SanitizeUntrustedString());
                throw new DeadLetterException(ex.Message);
            }

        }
    }
}