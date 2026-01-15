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
using LantanaGroup.Link.Report.Domain.Queries;
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

        private readonly PatientAggregator _patientAggregator;
        private readonly BlobStorageService _blobStorageService;
        private readonly ReadyForValidationProducer _readyForValidationProducer;
        private readonly ReportManifestProducer _reportManifestProducer;
        private readonly AuditableEventOccurredProducer _auditableEventOccurredProducer;

        private string Name => this.GetType().Name;

        public MeasureReportGeneratedListener(
            ILogger<MeasureReportGeneratedListener> logger,
            IKafkaConsumerFactory<Null, MeasureReportGeneratedValue> kafkaConsumerFactory,
            ITransientExceptionHandler<Null, MeasureReportGeneratedValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<Null, MeasureReportGeneratedValue> deadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory,
            PatientAggregator patientAggregator,
            BlobStorageService blobStorageService,
            ReadyForValidationProducer readyForValidationProducer,
            ReportManifestProducer reportManifestProducer,
            AuditableEventOccurredProducer auditableEventOccurredProducer)
        {

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaConsumerFactory = kafkaConsumerFactory ?? throw new ArgumentException(nameof(kafkaConsumerFactory));

            _serviceScopeFactory = serviceScopeFactory;

            _transientExceptionHandler = transientExceptionHandler ?? throw new ArgumentException(nameof(transientExceptionHandler));
            _deadLetterExceptionHandler = deadLetterExceptionHandler ?? throw new ArgumentException(nameof(deadLetterExceptionHandler));

            _transientExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _transientExceptionHandler.Topic = nameof(KafkaTopic.MeasureReportGenerated) + "-Retry";

            _deadLetterExceptionHandler.ServiceName = ReportConstants.ServiceName;
            _deadLetterExceptionHandler.Topic = nameof(KafkaTopic.MeasureReportGenerated) + "-Error";
            _patientAggregator = patientAggregator;
            _blobStorageService = blobStorageService;
            _readyForValidationProducer = readyForValidationProducer;
            _reportManifestProducer = reportManifestProducer;
            _auditableEventOccurredProducer = auditableEventOccurredProducer;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
        }

        private async Task StartConsumerLoop(CancellationToken cancellationToken)
        {
            var consumerConfig = new ConsumerConfig()
            {
                GroupId = ReportConstants.ServiceName,
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
                                //TODO: Look into if this is needed
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
                throw new DeadLetterException($"MeasureReportGenerated event value segment missing");
            }

            if (!result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue))
            {
                throw new DeadLetterException("Correlation Id missing");
            }

            using var scope = _serviceScopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
            var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();
            var reportResourceManager = scope.ServiceProvider.GetRequiredService<IReportResourceManager>();
            var reportPopulationManager = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();

            var correlationId = Encoding.UTF8.GetString(headerValue);

            var reportEntry = await reportEntryManager.UpdateAsyncWithConsumerResult(result.Message.Value);
            var readyForValidation = reportEntry.MeasureReportList.All(x => x.Status == Domain.Enums.MeasureReportStatus.NotReportable || x.Status == Domain.Enums.MeasureReportStatus.ReadyForValidation);


            var schedule = await reportScheduledManager.GetReportSchedule(result.Message.Value.FacilityId, result.Message.Value.ReportTrackingId, cancellationToken);

            if (schedule == null)
            {
                throw new DeadLetterException($"No scheduled report record was found (ReportId = {result.Message.Value.ReportTrackingId}, FacilityId = {result.Message.Value.FacilityId}).");
            }

            if (!readyForValidation)
            {
                //TODO: Follow up on this logic
                await _reportManifestProducer.Produce(schedule, correlationId);
                return;
            }

            AggregateResult aggregateResult;

            try
            {
                aggregateResult = await _patientAggregator.AggregateToABS(result.Message.Value.PatientId, schedule);
            }
            catch (Exception ex) 
            {
                throw new DeadLetterException(ex.Message);
            }

            await reportEntryManager.UpdateAsyncWithAggregateResult(reportEntry, aggregateResult);
            await reportResourceManager.AddAsyncWithAggregateResult(facilityId, result.Message.Value.ReportTrackingId, result.Message.Value.PatientId, aggregateResult, cancellationToken);

            foreach (var aggregateMeasureReport in aggregateResult.MeasureReportResults)
            {
                var populationModel = await reportPopulationManager.SingleOrDefaultAsync(x => x.ReportScheduleId == result.Message.Value.ReportTrackingId && x.Measure == result.Message.Value.ReportType);

                if (populationModel == null)
                {
                    await reportPopulationManager.AddAsyncWithAggregateResult(result.Message.Value.FacilityId, result.Message.Value.ReportTrackingId, aggregateMeasureReport, cancellationToken);
                    continue;
                }

                await reportPopulationManager.UpdateAsyncWithAggregateResult(populationModel, aggregateMeasureReport, cancellationToken);
            }

            try
            {
                await _readyForValidationProducer.Produce(schedule.Id, schedule.ReportTypes, schedule.FacilityId, result.Message.Value.PatientId, aggregateResult.Uri.AbsolutePath, correlationId);
            }
            catch (ProduceException<ReadyForValidationKey, ReadyForValidationValue> ex)
            {
                //TODO: Add logic
                //TODO: Test 
                _logger.LogError(ex, "An error was encountered generating a MeasureReportGenerated event.\n\tFacilityId: {facilityId}\n\t", schedule.FacilityId);
            }
        }
    }
}