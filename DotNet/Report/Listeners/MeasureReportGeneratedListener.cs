using Confluent.Kafka;
using Confluent.Kafka.Extensions.Diagnostics;
using Google.Protobuf.WellKnownTypes;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
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
using LantanaGroup.Link.Shared.Settings;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;
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

        private readonly PatientReportSubmissionBundler _patientReportSubmissionBundler;
        private readonly BlobStorageService _blobStorageService;
        private readonly ReadyForValidationProducer _readyForValidationProducer;
        private readonly ReportManifestProducer _reportManifestProducer;
        private readonly AuditableEventOccurredProducer _auditableEventOccurredProducer;
        private readonly IReportEntryStatusManager _reportEntryManager;
        private readonly IReportScheduledManager _reportScheduledManager;

        private string Name => this.GetType().Name;

        public MeasureReportGeneratedListener(
            ILogger<MeasureReportGeneratedListener> logger,
            IKafkaConsumerFactory<Null, MeasureReportGeneratedValue> kafkaConsumerFactory,
            ITransientExceptionHandler<Null, MeasureReportGeneratedValue> transientExceptionHandler,
            IDeadLetterExceptionHandler<Null, MeasureReportGeneratedValue> deadLetterExceptionHandler,
            IServiceScopeFactory serviceScopeFactory,
            PatientReportSubmissionBundler patientReportSubmissionBundler,
            BlobStorageService blobStorageService,
            ReadyForValidationProducer readyForValidationProducer,
            ReportManifestProducer reportManifestProducer,
            AuditableEventOccurredProducer auditableEventOccurredProducer, 
            IReportEntryStatusManager reportEntryManager,
            IReportScheduledManager reportScheduledManager)
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
            _patientReportSubmissionBundler = patientReportSubmissionBundler;
            _blobStorageService = blobStorageService;
            _readyForValidationProducer = readyForValidationProducer;
            _reportManifestProducer = reportManifestProducer;
            _auditableEventOccurredProducer = auditableEventOccurredProducer;
            _reportEntryManager = reportEntryManager;
            _reportScheduledManager = reportScheduledManager;
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
                    try
                    {
                        await consumer.ConsumeWithInstrumentation(async (result, consumeCancellationToken) =>
                        {
                            if (!result.Message.Headers.TryGetLastBytes("X-Correlation-Id", out var headerValue)) 
                            { 
                                //TODO: Add error
                            }

                            var correlationId = Encoding.UTF8.GetString(headerValue);

                            var reportEntry = await _reportEntryManager.GetPatientEntry(result.Value.ReportTrackingId, result.Value.ReportType, result.Value.PatientId);

                            //TODO: Add null check 

                            reportEntry.MeasureReportFileName = result.Value.MeasureReportFileName;
                            reportEntry.MeasureReportUri = result.Value.MeasureReportURI;

                            if (result.Value.IsReportable)
                            {
                                reportEntry.Status = PatientSubmissionStatus.ReadyForValidation;
                            }
                            else 
                            {
                                reportEntry.Status = PatientSubmissionStatus.NotReportable;
                                reportEntry.ValidationStatus = ValidationStatus.NotReportable;
                            }

                            await _reportEntryManager.UpdateAsync(reportEntry, cancellationToken);

                            var entries = await _reportEntryManager.FindAsync(x => x.PatientId == result.Value.PatientId && x.FacilityId == result.Value.FacilityId && x.ReportScheduleId == result.Value.ReportTrackingId);

                           var readyForValidation = entries.All(e =>
                                e.Status == PatientSubmissionStatus.NotReportable ||
                                e.Status == PatientSubmissionStatus.ReadyForValidation) &&
                                entries.Any(e => e.Status == PatientSubmissionStatus.ReadyForValidation);


                            var schedule = await _reportScheduledManager.GetReportSchedule(result.Value.FacilityId, result.Value.ReportTrackingId, cancellationToken);

                            //TODO: Follow up on this logic
                            if (!readyForValidation)
                            {
                                await _reportManifestProducer.Produce(schedule, correlationId);
                                return;
                            }

                            Uri ndjson_blob_uri = await _patientReportSubmissionBundler.GenerateBundleToABS(result.Value.PatientId, result.Value.ReportTrackingId);

                            foreach (var ent in entries.Where(s => s.Status == PatientSubmissionStatus.ReadyForValidation))
                            {
                                ent.AggregateReportUri = ndjson_blob_uri.AbsoluteUri;
                                //TODO: Add AggregateFileName
                                ent.ModifyDate = DateTime.UtcNow;
                                await _reportEntryManager.UpdateAsync(ent, cancellationToken);
                            }

                            try
                            {
                                await _readyForValidationProducer.Produce(schedule.Id, schedule.ReportTypes, schedule.FacilityId, result.Value.PatientId, ndjson_blob_uri.AbsolutePath, correlationId);
                            }
                            catch (ProduceException<ReadyForValidationKey, ReadyForValidationValue> ex)
                            {
                                //TODO: Add logic
                            }           
                        }, cancellationToken);
                    }
                    catch (ConsumeException ex)
                    {

                    }
                    catch (OperationCanceledException oce)
                    {

                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
            catch (OperationCanceledException oce)
            {
            }
        }
    }
}
